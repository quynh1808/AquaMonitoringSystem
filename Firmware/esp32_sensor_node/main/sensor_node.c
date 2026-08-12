#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include "cJSON.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_mac.h"
#include "esp_random.h"
#include "esp_sntp.h"
#include "esp_wifi.h"
#include "mqtt_client.h"
#include "nvs.h"
#include "nvs_flash.h"
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/task.h"

/* This firmware simulates probe readings at the edge. AquaOps receives the exact same MQTT
 * contract that physical probe drivers will use later; only update_sensor_values() is replaced. */
#define WIFI_CONNECTED_BIT BIT0
#define MQTT_CONNECTED_BIT BIT1
#define PUBLISH_INTERVAL_MS 30000
#define FIRMWARE_VERSION "edge-sim-0.1.0"

static const char *TAG = "aqua_sensor";
static EventGroupHandle_t connection_events;
static esp_mqtt_client_handle_t mqtt_client;
static uint32_t boot_counter;
static uint32_t sample_counter;
static char node_id[24];

typedef struct {
    const char *metric;
    const char *unit;
    double value;
    double minimum;
    double maximum;
    double max_step;
} sensor_value_t;

static sensor_value_t sensors[] = {
    { "dissolvedOxygen", "mg/L", 6.4, 4.2, 8.5, 0.18 },
    { "ph", "pH", 7.8, 7.0, 8.6, 0.04 },
    { "temperature", "°C", 29.0, 25.0, 33.5, 0.12 },
    { "salinity", "ppt", 15.0, 8.0, 25.0, 0.15 },
    { "ammonia", "mg/L", 0.08, 0.01, 0.30, 0.01 },
    { "waterLevel", "m", 1.50, 1.20, 1.80, 0.01 }
};

static void wifi_event_handler(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    if (base == WIFI_EVENT && id == WIFI_EVENT_STA_START) esp_wifi_connect();
    else if (base == WIFI_EVENT && id == WIFI_EVENT_STA_DISCONNECTED) {
        xEventGroupClearBits(connection_events, WIFI_CONNECTED_BIT);
        esp_wifi_connect();
    } else if (base == IP_EVENT && id == IP_EVENT_STA_GOT_IP) {
        xEventGroupSetBits(connection_events, WIFI_CONNECTED_BIT);
    }
}

static void mqtt_event_handler(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    if (id == MQTT_EVENT_CONNECTED) xEventGroupSetBits(connection_events, MQTT_CONNECTED_BIT);
    else if (id == MQTT_EVENT_DISCONNECTED) xEventGroupClearBits(connection_events, MQTT_CONNECTED_BIT);
}

static void start_wifi(void)
{
    ESP_ERROR_CHECK(esp_netif_init());
    ESP_ERROR_CHECK(esp_event_loop_create_default());
    esp_netif_create_default_wifi_sta();
    wifi_init_config_t init = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK(esp_wifi_init(&init));
    ESP_ERROR_CHECK(esp_event_handler_register(WIFI_EVENT, ESP_EVENT_ANY_ID, wifi_event_handler, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_STA_GOT_IP, wifi_event_handler, NULL));
    wifi_config_t config = { 0 };
    snprintf((char *)config.sta.ssid, sizeof(config.sta.ssid), "%s", CONFIG_AQUA_WIFI_SSID);
    snprintf((char *)config.sta.password, sizeof(config.sta.password), "%s", CONFIG_AQUA_WIFI_PASSWORD);
    config.sta.threshold.authmode = WIFI_AUTH_WPA2_PSK;
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &config));
    ESP_ERROR_CHECK(esp_wifi_start());
}

static void start_clock(void)
{
    /* UTC timestamps make server-side ordering and backfill checks independent of farm timezone. */
    esp_sntp_config_t config = ESP_NETIF_SNTP_DEFAULT_CONFIG("pool.ntp.org");
    esp_netif_sntp_init(&config);
}

static void load_identity(void)
{
    uint8_t mac[6];
    ESP_ERROR_CHECK(esp_read_mac(mac, ESP_MAC_WIFI_STA));
    snprintf(node_id, sizeof(node_id), "sensor-%02x%02x%02x", mac[3], mac[4], mac[5]);

    /* A boot counter forms the high half of sequenceNumber, preventing a reboot from reusing
     * sequence values that the server has already accepted. */
    nvs_handle_t nvs;
    ESP_ERROR_CHECK(nvs_open("aquaops", NVS_READWRITE, &nvs));
    if (nvs_get_u32(nvs, "boot_count", &boot_counter) != ESP_OK) boot_counter = 0;
    boot_counter++;
    ESP_ERROR_CHECK(nvs_set_u32(nvs, "boot_count", boot_counter));
    ESP_ERROR_CHECK(nvs_commit(nvs));
    nvs_close(nvs);
}

static void update_sensor_values(void)
{
    for (size_t i = 0; i < sizeof(sensors) / sizeof(sensors[0]); ++i) {
        const double random_unit = ((double)(esp_random() % 2001) / 1000.0) - 1.0;
        sensors[i].value = fmin(sensors[i].maximum,
            fmax(sensors[i].minimum, sensors[i].value + random_unit * sensors[i].max_step));
    }
}

static bool utc_timestamp(char *buffer, size_t size)
{
    time_t now;
    time(&now);
    if (now < 1700000000) return false; /* Do not publish fabricated 1970 timestamps before SNTP. */
    struct tm utc;
    gmtime_r(&now, &utc);
    return strftime(buffer, size, "%Y-%m-%dT%H:%M:%SZ", &utc) > 0;
}

static void publish_metric(const sensor_value_t *sensor, uint64_t sequence, const char *timestamp)
{
    char topic[160];
    snprintf(topic, sizeof(topic), "aquaculture/%s/%s/sensors/%s/telemetry",
        CONFIG_AQUA_FARM_ID, CONFIG_AQUA_POND_ID, sensor->metric);
    char event_id[96];
    snprintf(event_id, sizeof(event_id), "%s-%lu-%lu-%s", node_id,
        (unsigned long)boot_counter, (unsigned long)sample_counter, sensor->metric);

    cJSON *root = cJSON_CreateObject();
    char sensor_id[96];
    snprintf(sensor_id, sizeof(sensor_id), "sensor_%s_%s", CONFIG_AQUA_POND_ID, sensor->metric);
    cJSON_AddStringToObject(root, "sensorId", sensor_id);
    cJSON_AddStringToObject(root, "pondId", CONFIG_AQUA_POND_ID);
    cJSON_AddStringToObject(root, "metric", sensor->metric);
    cJSON_AddNumberToObject(root, "value", sensor->value);
    cJSON_AddStringToObject(root, "unit", sensor->unit);
    cJSON_AddStringToObject(root, "timestampUtc", timestamp);
    cJSON_AddStringToObject(root, "quality", "good");
    cJSON_AddStringToObject(root, "firmwareVersion", FIRMWARE_VERSION);
    cJSON_AddStringToObject(root, "eventId", event_id);
    cJSON_AddNumberToObject(root, "sequenceNumber", (double)sequence);
    cJSON_AddBoolToObject(root, "isBackfill", false);
    char *payload = cJSON_PrintUnformatted(root);
    /* QoS 1 and the MQTT outbox retain messages during short Wi-Fi interruptions. */
    esp_mqtt_client_enqueue(mqtt_client, topic, payload, 0, 1, 0, true);
    free(payload);
    cJSON_Delete(root);
}

static void publisher_task(void *arg)
{
    while (true) {
        xEventGroupWaitBits(connection_events, MQTT_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);
        char timestamp[32];
        if (utc_timestamp(timestamp, sizeof(timestamp))) {
            update_sensor_values();
            sample_counter++;
            const uint64_t sequence = ((uint64_t)boot_counter << 32) | sample_counter;
            for (size_t i = 0; i < sizeof(sensors) / sizeof(sensors[0]); ++i)
                publish_metric(&sensors[i], sequence, timestamp);
        } else ESP_LOGW(TAG, "SNTP chưa đồng bộ; bỏ qua chu kỳ publish");
        vTaskDelay(pdMS_TO_TICKS(PUBLISH_INTERVAL_MS));
    }
}

void app_main(void)
{
    ESP_ERROR_CHECK(nvs_flash_init());
    connection_events = xEventGroupCreate();
    load_identity();
    start_wifi();
    xEventGroupWaitBits(connection_events, WIFI_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);
    start_clock();

    const esp_mqtt_client_config_t mqtt_config = {
        .broker.address.uri = CONFIG_AQUA_MQTT_URI,
        .credentials.username = CONFIG_AQUA_MQTT_USERNAME,
        .credentials.authentication.password = CONFIG_AQUA_MQTT_PASSWORD,
        .credentials.client_id = node_id
    };
    mqtt_client = esp_mqtt_client_init(&mqtt_config);
    ESP_ERROR_CHECK(esp_mqtt_client_register_event(mqtt_client, ESP_EVENT_ANY_ID, mqtt_event_handler, NULL));
    ESP_ERROR_CHECK(esp_mqtt_client_start(mqtt_client));
    xTaskCreate(publisher_task, "sensor_publish", 6144, NULL, 5, NULL);
}

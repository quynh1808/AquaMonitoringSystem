#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include "aqua_provisioning.h"
#include "cJSON.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_random.h"
#include "esp_netif_sntp.h"
#include "esp_timer.h"
#include "esp_wifi.h"
#include "mqtt_client.h"
#include "nvs.h"
#include "nvs_flash.h"
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/semphr.h"
#include "freertos/task.h"

/* Edge Simulation uses the production protocol. Replacing simulate_value() with real probe
 * drivers does not change identity, configuration, health or durable delivery semantics. */
#define WIFI_CONNECTED_BIT BIT0
#define MQTT_CONNECTED_BIT BIT1
#define CONFIG_APPLIED_BIT BIT2
#define SYNC_ACK_BIT BIT3
#define FIRMWARE_VERSION "edge-sim-0.2.0"
#define CHANNEL_COUNT 6
#define RECORD_PAYLOAD_SIZE 640

static const char *TAG = "aqua_sensor";
static EventGroupHandle_t events;
static SemaphoreHandle_t state_lock;
static esp_mqtt_client_handle_t mqtt_client;
static aqua_bootstrap_config_t bootstrap;
static uint32_t boot_counter, sample_counter, desired_version;
static char last_ack_event_id[96];

typedef struct {
    char channel_id[32]; char sensor_id[64]; char installation_id[40];
    const char *metric; const char *unit; const char *driver;
    double value, simulation_min, simulation_max, max_step, physical_min, physical_max;
    uint32_t interval_seconds; int64_t last_sample_us;
} sensor_channel_t;

typedef struct { char event_id[96]; char payload[RECORD_PAYLOAD_SIZE]; } durable_record_t;
typedef struct { char sensor_id[64]; char installation_id[40]; uint32_t interval_seconds; } persisted_channel_t;

static sensor_channel_t channels[CHANNEL_COUNT] = {
    { "do-01", "", "", "dissolvedOxygen", "mg/L", "sim-do", 6.4, 4.2, 8.5, .18, 0, 20, 0, 0 },
    { "ph-01", "", "", "ph", "pH", "sim-ph", 7.8, 7.0, 8.6, .04, 0, 14, 0, 0 },
    { "temperature-01", "", "", "temperature", "°C", "sim-temperature", 29, 25, 33.5, .12, -10, 85, 0, 0 },
    { "salinity-01", "", "", "salinity", "ppt", "sim-salinity", 15, 8, 25, .15, 0, 70, 0, 0 },
    { "ammonia-01", "", "", "ammonia", "mg/L", "sim-ammonia", .08, .01, .30, .01, 0, 10, 0, 0 },
    { "water-level-01", "", "", "waterLevel", "m", "sim-level", 1.5, 1.2, 1.8, .01, 0, 10, 0, 0 }
};

static void topic(char *target, size_t size, const char *suffix)
{
    snprintf(target, size, "aquaculture/%s/%s/nodes/%s/%s", bootstrap.farm_id,
        bootstrap.pond_id, bootstrap.node_id, suffix);
}

static bool utc_timestamp(char *buffer, size_t size)
{
    time_t now; time(&now);
    if (now < 1700000000) return false; /* Never invent UTC after a cold offline boot. */
    struct tm utc; gmtime_r(&now, &utc);
    return strftime(buffer, size, "%Y-%m-%dT%H:%M:%SZ", &utc) > 0;
}

static void load_runtime_state(void)
{
    nvs_handle_t nvs; ESP_ERROR_CHECK(nvs_open("aqua_node", NVS_READWRITE, &nvs));
    if (nvs_get_u32(nvs, "boot_count", &boot_counter) != ESP_OK) boot_counter = 0;
    boot_counter++; nvs_set_u32(nvs, "boot_count", boot_counter);
    nvs_get_u32(nvs, "cfg_version", &desired_version);
    persisted_channel_t persisted[CHANNEL_COUNT] = { 0 }; size_t size = sizeof(persisted);
    if (nvs_get_blob(nvs, "channels", persisted, &size) != ESP_OK || size != sizeof(persisted)) {
        for (size_t i = 0; i < CHANNEL_COUNT; ++i) { channels[i].sensor_id[0] = 0; channels[i].interval_seconds = 0; }
    } else for (size_t i = 0; i < CHANNEL_COUNT; ++i) {
        snprintf(channels[i].sensor_id, sizeof(channels[i].sensor_id), "%s", persisted[i].sensor_id);
        snprintf(channels[i].installation_id, sizeof(channels[i].installation_id), "%s", persisted[i].installation_id);
        channels[i].interval_seconds = persisted[i].interval_seconds;
    }
    ESP_ERROR_CHECK(nvs_commit(nvs)); nvs_close(nvs);
}

static void save_channel_configuration(void)
{
    nvs_handle_t nvs; ESP_ERROR_CHECK(nvs_open("aqua_node", NVS_READWRITE, &nvs));
    ESP_ERROR_CHECK(nvs_set_u32(nvs, "cfg_version", desired_version));
    persisted_channel_t persisted[CHANNEL_COUNT] = { 0 };
    for (size_t i = 0; i < CHANNEL_COUNT; ++i) {
        snprintf(persisted[i].sensor_id, sizeof(persisted[i].sensor_id), "%s", channels[i].sensor_id);
        snprintf(persisted[i].installation_id, sizeof(persisted[i].installation_id), "%s", channels[i].installation_id);
        persisted[i].interval_seconds = channels[i].interval_seconds;
    }
    ESP_ERROR_CHECK(nvs_set_blob(nvs, "channels", persisted, sizeof(persisted)));
    ESP_ERROR_CHECK(nvs_commit(nvs)); nvs_close(nvs);
}

static void queue_record(const durable_record_t *record)
{
    nvs_handle_t nvs; ESP_ERROR_CHECK(nvs_open("aqua_queue", NVS_READWRITE, &nvs));
    uint32_t head = 0, tail = 0; nvs_get_u32(nvs, "head", &head); nvs_get_u32(nvs, "tail", &tail);
    if (tail - head >= CONFIG_AQUA_FLASH_BUFFER_CAPACITY) {
        /* Bounded retention prevents a full flash from breaking the node. Overflow is explicit. */
        head++; nvs_set_u32(nvs, "head", head); ESP_LOGE(TAG, "Offline buffer overflow; oldest record discarded");
    }
    char key[16]; snprintf(key, sizeof(key), "q%08lx", (unsigned long)(tail % CONFIG_AQUA_FLASH_BUFFER_CAPACITY));
    ESP_ERROR_CHECK(nvs_set_blob(nvs, key, record, sizeof(*record)));
    ESP_ERROR_CHECK(nvs_set_u32(nvs, "tail", tail + 1)); ESP_ERROR_CHECK(nvs_commit(nvs)); nvs_close(nvs);
}

static bool queue_peek(durable_record_t *record, uint32_t *index)
{
    nvs_handle_t nvs; if (nvs_open("aqua_queue", NVS_READONLY, &nvs) != ESP_OK) return false;
    uint32_t head = 0, tail = 0; nvs_get_u32(nvs, "head", &head); nvs_get_u32(nvs, "tail", &tail);
    if (head == tail) { nvs_close(nvs); return false; }
    char key[16]; snprintf(key, sizeof(key), "q%08lx", (unsigned long)(head % CONFIG_AQUA_FLASH_BUFFER_CAPACITY));
    size_t size = sizeof(*record); bool ok = nvs_get_blob(nvs, key, record, &size) == ESP_OK && size == sizeof(*record);
    *index = head; nvs_close(nvs); return ok;
}

static void queue_ack(uint32_t index)
{
    nvs_handle_t nvs; ESP_ERROR_CHECK(nvs_open("aqua_queue", NVS_READWRITE, &nvs));
    uint32_t head = 0; nvs_get_u32(nvs, "head", &head);
    if (head == index) { char key[16]; snprintf(key, sizeof(key), "q%08lx", (unsigned long)(head % CONFIG_AQUA_FLASH_BUFFER_CAPACITY)); nvs_erase_key(nvs, key); nvs_set_u32(nvs, "head", head + 1); nvs_commit(nvs); }
    nvs_close(nvs);
}

static void publish_json(const char *suffix, const char *payload, int qos, bool retain)
{
    char full_topic[192]; topic(full_topic, sizeof(full_topic), suffix);
    esp_mqtt_client_enqueue(mqtt_client, full_topic, payload, 0, qos, retain, true);
}

static void publish_status(bool online)
{
    char payload[192]; snprintf(payload, sizeof(payload),
        "{\"online\":%s,\"health\":\"%s\",\"firmwareVersion\":\"%s\",\"bootId\":%lu}",
        online ? "true" : "false", online ? "healthy" : "offline", FIRMWARE_VERSION, (unsigned long)boot_counter);
    publish_json("status", payload, 1, true);
}

static void publish_capabilities(void)
{
    cJSON *root = cJSON_CreateObject(); cJSON_AddStringToObject(root, "firmwareVersion", FIRMWARE_VERSION);
    cJSON *items = cJSON_AddArrayToObject(root, "sensorChannels");
    for (size_t i = 0; i < CHANNEL_COUNT; ++i) {
        cJSON *item = cJSON_CreateObject(); cJSON_AddStringToObject(item, "channelId", channels[i].channel_id);
        cJSON_AddStringToObject(item, "metric", channels[i].metric); cJSON_AddStringToObject(item, "unit", channels[i].unit);
        cJSON_AddNumberToObject(item, "physicalMin", channels[i].physical_min); cJSON_AddNumberToObject(item, "physicalMax", channels[i].physical_max);
        cJSON_AddStringToObject(item, "driver", channels[i].driver); cJSON_AddStringToObject(item, "driverVersion", FIRMWARE_VERSION);
        cJSON_AddStringToObject(item, "interface", "edge-simulation"); cJSON_AddItemToArray(items, item);
    }
    char *payload = cJSON_PrintUnformatted(root); publish_json("capabilities", payload, 1, true); free(payload); cJSON_Delete(root);
}

static void publish_health(const sensor_channel_t *channel, const char *status, const char *error)
{
    if (!channel->sensor_id[0]) return;
    char suffix[128]; snprintf(suffix, sizeof(suffix), "sensors/%s/health", channel->sensor_id);
    cJSON *root = cJSON_CreateObject(); cJSON_AddStringToObject(root, "sensorId", channel->sensor_id);
    cJSON_AddStringToObject(root, "installationId", channel->installation_id); cJSON_AddStringToObject(root, "status", status);
    cJSON_AddStringToObject(root, "driverStatus", error ? "fault" : "healthy"); cJSON_AddStringToObject(root, "busStatus", "healthy");
    cJSON_AddStringToObject(root, "quality", error ? "invalid" : "good"); cJSON_AddStringToObject(root, "driverVersion", FIRMWARE_VERSION);
    cJSON_AddNumberToObject(root, "physicalMin", channel->physical_min); cJSON_AddNumberToObject(root, "physicalMax", channel->physical_max);
    if (error) cJSON_AddStringToObject(root, "errorMessage", error);
    char *payload = cJSON_PrintUnformatted(root); publish_json(suffix, payload, 1, false); free(payload); cJSON_Delete(root);
}

static void report_configuration(const char *command_id, bool accepted, const char *error)
{
    cJSON *root = cJSON_CreateObject(); cJSON_AddStringToObject(root, "commandId", command_id);
    cJSON_AddNumberToObject(root, "version", desired_version); cJSON_AddBoolToObject(root, "accepted", accepted);
    if (error) cJSON_AddStringToObject(root, "error", error);
    char *payload = cJSON_PrintUnformatted(root); publish_json("config/reported", payload, 1, true); free(payload); cJSON_Delete(root);
}

static void apply_desired_configuration(const char *payload, int length)
{
    cJSON *root = cJSON_ParseWithLength(payload, length); if (!root) return;
    cJSON *command = cJSON_GetObjectItem(root, "commandId"), *version = cJSON_GetObjectItem(root, "version"), *items = cJSON_GetObjectItem(root, "sensors");
    if (!cJSON_IsString(command) || !cJSON_IsNumber(version) || !cJSON_IsArray(items)) { cJSON_Delete(root); return; }
    xSemaphoreTake(state_lock, portMAX_DELAY);
    for (size_t i = 0; i < CHANNEL_COUNT; ++i) { channels[i].sensor_id[0] = 0; channels[i].installation_id[0] = 0; channels[i].interval_seconds = 0; }
    cJSON *item;
    cJSON_ArrayForEach(item, items) {
        cJSON *channel_id = cJSON_GetObjectItem(item, "channelId"), *sensor_id = cJSON_GetObjectItem(item, "sensorId");
        cJSON *installation_id = cJSON_GetObjectItem(item, "installationId"), *interval = cJSON_GetObjectItem(item, "intervalSeconds");
        if (!cJSON_IsString(channel_id) || !cJSON_IsString(sensor_id) || !cJSON_IsNumber(interval)) continue;
        for (size_t i = 0; i < CHANNEL_COUNT; ++i) if (!strcmp(channels[i].channel_id, channel_id->valuestring)) {
            snprintf(channels[i].sensor_id, sizeof(channels[i].sensor_id), "%s", sensor_id->valuestring);
            if (cJSON_IsString(installation_id)) snprintf(channels[i].installation_id, sizeof(channels[i].installation_id), "%s", installation_id->valuestring);
            channels[i].interval_seconds = interval->valuedouble < 0 ? 0 : (uint32_t)interval->valuedouble; break;
        }
    }
    desired_version = (uint32_t)version->valuedouble; save_channel_configuration(); xSemaphoreGive(state_lock);
    report_configuration(command->valuestring, true, NULL); xEventGroupSetBits(events, CONFIG_APPLIED_BIT);
    for (size_t i = 0; i < CHANNEL_COUNT; ++i) publish_health(&channels[i], channels[i].interval_seconds ? "initializing" : "not-configured", NULL);
    cJSON_Delete(root);
}

static void handle_sync_ack(const char *payload, int length)
{
    cJSON *root = cJSON_ParseWithLength(payload, length); if (!root) return;
    cJSON *ids = cJSON_GetObjectItem(root, "eventIds"), *item;
    cJSON_ArrayForEach(item, ids) if (cJSON_IsString(item) && !strcmp(item->valuestring, last_ack_event_id)) xEventGroupSetBits(events, SYNC_ACK_BIT);
    cJSON_Delete(root);
}

static void mqtt_event_handler(void *arg, esp_event_base_t base, int32_t id, void *event_data)
{
    esp_mqtt_event_handle_t event = event_data;
    if (id == MQTT_EVENT_CONNECTED) {
        xEventGroupSetBits(events, MQTT_CONNECTED_BIT); char desired[192], ack[192];
        topic(desired, sizeof(desired), "config/desired"); topic(ack, sizeof(ack), "sync/ack");
        esp_mqtt_client_subscribe(mqtt_client, desired, 1); esp_mqtt_client_subscribe(mqtt_client, ack, 1);
        publish_status(true); publish_capabilities();
        char request[128]; snprintf(request, sizeof(request), "{\"reason\":\"boot-or-reconnect\",\"reportedVersion\":%lu}", (unsigned long)desired_version);
        publish_json("config/request", request, 1, false);
    } else if (id == MQTT_EVENT_DISCONNECTED) xEventGroupClearBits(events, MQTT_CONNECTED_BIT);
    else if (id == MQTT_EVENT_DATA) {
        char desired[192], ack[192]; topic(desired, sizeof(desired), "config/desired"); topic(ack, sizeof(ack), "sync/ack");
        if ((int)strlen(desired) == event->topic_len && !strncmp(event->topic, desired, event->topic_len)) apply_desired_configuration(event->data, event->data_len);
        else if ((int)strlen(ack) == event->topic_len && !strncmp(event->topic, ack, event->topic_len)) handle_sync_ack(event->data, event->data_len);
    }
}

static void wifi_event_handler(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    if (base == WIFI_EVENT && id == WIFI_EVENT_STA_START) esp_wifi_connect();
    else if (base == WIFI_EVENT && id == WIFI_EVENT_STA_DISCONNECTED) { xEventGroupClearBits(events, WIFI_CONNECTED_BIT); esp_wifi_connect(); }
    else if (base == IP_EVENT && id == IP_EVENT_STA_GOT_IP) xEventGroupSetBits(events, WIFI_CONNECTED_BIT);
}

static void start_wifi(void)
{
    ESP_ERROR_CHECK(esp_netif_init()); ESP_ERROR_CHECK(esp_event_loop_create_default()); esp_netif_create_default_wifi_sta();
    wifi_init_config_t init = WIFI_INIT_CONFIG_DEFAULT(); ESP_ERROR_CHECK(esp_wifi_init(&init));
    ESP_ERROR_CHECK(esp_event_handler_register(WIFI_EVENT, ESP_EVENT_ANY_ID, wifi_event_handler, NULL));
    ESP_ERROR_CHECK(esp_event_handler_register(IP_EVENT, IP_EVENT_STA_GOT_IP, wifi_event_handler, NULL));
    wifi_config_t config = { 0 };
    const size_t ssid_length = strlen(bootstrap.wifi_ssid);
    const size_t password_length = strlen(bootstrap.wifi_password);
    if (ssid_length > sizeof(config.sta.ssid) || password_length > sizeof(config.sta.password)) {
        ESP_LOGE(TAG, "Wi-Fi bootstrap exceeds ESP32 SSID/password limits");
        ESP_ERROR_CHECK(ESP_ERR_INVALID_SIZE);
        return;
    }
    /* A 32-byte SSID or 64-byte WPA key may fill the whole ESP-IDF field without a trailing NUL. */
    memcpy(config.sta.ssid, bootstrap.wifi_ssid, ssid_length);
    memcpy(config.sta.password, bootstrap.wifi_password, password_length);
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA)); ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &config)); ESP_ERROR_CHECK(esp_wifi_start());
}

static void create_record(sensor_channel_t *channel, bool was_offline)
{
    char timestamp[32]; if (!utc_timestamp(timestamp, sizeof(timestamp))) { publish_health(channel, "degraded", "UTC chưa đồng bộ"); return; }
    const double random_unit = ((double)(esp_random() % 2001) / 1000.0) - 1.0;
    channel->value = fmin(channel->simulation_max, fmax(channel->simulation_min, channel->value + random_unit * channel->max_step));
    sample_counter++; uint64_t sequence = ((uint64_t)boot_counter << 32) | sample_counter;
    durable_record_t record = { 0 }; snprintf(record.event_id, sizeof(record.event_id), "%s-%lu-%lu-%s", bootstrap.node_id,
        (unsigned long)boot_counter, (unsigned long)sample_counter, channel->channel_id);
    cJSON *root = cJSON_CreateObject(); cJSON_AddStringToObject(root, "sensorId", channel->sensor_id);
    cJSON_AddStringToObject(root, "installationId", channel->installation_id); cJSON_AddStringToObject(root, "metric", channel->metric);
    cJSON_AddNumberToObject(root, "value", channel->value); cJSON_AddStringToObject(root, "unit", channel->unit);
    cJSON_AddStringToObject(root, "timestampUtc", timestamp); cJSON_AddStringToObject(root, "timeQuality", "synchronized");
    cJSON_AddStringToObject(root, "quality", "good"); cJSON_AddStringToObject(root, "firmwareVersion", FIRMWARE_VERSION);
    cJSON_AddStringToObject(root, "eventId", record.event_id); cJSON_AddNumberToObject(root, "sequenceNumber", (double)sequence);
    cJSON_AddBoolToObject(root, "isBackfill", was_offline); char *json = cJSON_PrintUnformatted(root);
    snprintf(record.payload, sizeof(record.payload), "%s", json); free(json); cJSON_Delete(root); queue_record(&record);
}

static void sampling_task(void *arg)
{
    while (true) {
        int64_t now_us = esp_timer_get_time(); bool offline = !(xEventGroupGetBits(events) & MQTT_CONNECTED_BIT);
        xSemaphoreTake(state_lock, portMAX_DELAY);
        for (size_t i = 0; i < CHANNEL_COUNT; ++i) if (channels[i].sensor_id[0] && channels[i].interval_seconds > 0 &&
            (channels[i].last_sample_us == 0 || now_us - channels[i].last_sample_us >= (int64_t)channels[i].interval_seconds * 1000000)) {
            create_record(&channels[i], offline); channels[i].last_sample_us = now_us;
        }
        xSemaphoreGive(state_lock); vTaskDelay(pdMS_TO_TICKS(250));
    }
}

static void sync_task(void *arg)
{
    while (true) {
        xEventGroupWaitBits(events, MQTT_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);
        durable_record_t record; uint32_t index;
        if (!queue_peek(&record, &index)) { vTaskDelay(pdMS_TO_TICKS(500)); continue; }
        snprintf(last_ack_event_id, sizeof(last_ack_event_id), "%s", record.event_id); xEventGroupClearBits(events, SYNC_ACK_BIT);
        char batch[RECORD_PAYLOAD_SIZE + 160]; snprintf(batch, sizeof(batch), "{\"batchId\":\"%s\",\"items\":[%s]}", record.event_id, record.payload);
        publish_json("sync/data", batch, 1, false);
        EventBits_t result = xEventGroupWaitBits(events, SYNC_ACK_BIT, pdTRUE, pdTRUE, pdMS_TO_TICKS(10000));
        if (result & SYNC_ACK_BIT) queue_ack(index); else ESP_LOGW(TAG, "Chưa có application ACK cho %s; giữ bản ghi trong flash", record.event_id);
        vTaskDelay(pdMS_TO_TICKS(250)); /* Throttle backfill so live operation is not starved. */
    }
}

void app_main(void)
{
    esp_err_t init = nvs_flash_init(); if (init == ESP_ERR_NVS_NO_FREE_PAGES || init == ESP_ERR_NVS_NEW_VERSION_FOUND) { ESP_ERROR_CHECK(nvs_flash_erase()); ESP_ERROR_CHECK(nvs_flash_init()); }
    aqua_provisioning_start_console(true);
    if (!aqua_provisioning_load(&bootstrap, true)) { ESP_LOGE(TAG, "Thiếu bootstrap NVS; provision qua UART rồi restart."); while (true) vTaskDelay(pdMS_TO_TICKS(1000)); }
    events = xEventGroupCreate(); state_lock = xSemaphoreCreateMutex(); load_runtime_state(); start_wifi();
    xEventGroupWaitBits(events, WIFI_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);
    /* UTC must be synchronized before records are accepted into the durable queue.
     * Production LANs without Internet should replace this host with a local NTP service. */
    esp_sntp_config_t clock = ESP_NETIF_SNTP_DEFAULT_CONFIG("pool.ntp.org");
    ESP_ERROR_CHECK(esp_netif_sntp_init(&clock));
    char will_topic[192]; topic(will_topic, sizeof(will_topic), "status");
    const esp_mqtt_client_config_t mqtt_config = {
        .broker.address.uri = bootstrap.mqtt_uri, .credentials.username = bootstrap.mqtt_username,
        .credentials.authentication.password = bootstrap.mqtt_password, .credentials.client_id = bootstrap.node_id,
        .session.last_will = { .topic = will_topic, .msg = "{\"online\":false,\"health\":\"offline\"}", .qos = 1, .retain = 1 }
    };
    mqtt_client = esp_mqtt_client_init(&mqtt_config); ESP_ERROR_CHECK(esp_mqtt_client_register_event(mqtt_client, ESP_EVENT_ANY_ID, mqtt_event_handler, NULL));
    ESP_ERROR_CHECK(esp_mqtt_client_start(mqtt_client)); xTaskCreate(sampling_task, "sensor_sample", 6144, NULL, 5, NULL); xTaskCreate(sync_task, "durable_sync", 6144, NULL, 5, NULL);
}

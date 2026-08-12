#include <errno.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <time.h>
#include <unistd.h>
#include "aqua_provisioning.h"
#include "esp_event.h"
#include "esp_log.h"
#include "esp_wifi.h"
#include "nvs_flash.h"
#include "freertos/FreeRTOS.h"
#include "freertos/event_groups.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#include "lwip/inet.h"

/* This is a deliberately small FX5U-compatible test endpoint, not a complete PLC emulator.
 * It implements FC01/FC03/FC05/FC06 required by AquaOps commissioning and config ACK. */
#define WIFI_CONNECTED_BIT BIT0
#define FX5_M_BASE 0x2000
#define FX5_M_COUNT 7680
#define COIL_BYTES ((FX5_M_COUNT + 7) / 8)
#define D_HEARTBEAT_ADDRESS 100
#define D_REGISTER_COUNT 256
#define M_CONFIG_REQUEST 50
#define M_APPLY_REQUEST 51
#define M_APPLY_ACK 52
#define M_CONFIG_ERROR 53
#define D_DESIRED_VERSION 110
#define D_APPLIED_VERSION 111
#define D_ERROR_CODE 112
#define M_MANUAL_START 100
#define M_MANUAL_STOP 101
#define M_LOCAL_OVERRIDE 200
#define M_REMOTE_ALLOWED 201
#define M_AUTO_REQUEST 210
#define M_AUTO_FEEDBACK 211
#define M_RUN_FEEDBACK 300
#define M_FAULT 400

static const char *TAG = "fx5u_sim";
static EventGroupHandle_t wifi_events;
static SemaphoreHandle_t register_lock;
static uint8_t coils[COIL_BYTES];
static uint16_t holding[D_REGISTER_COUNT];
static TickType_t last_master_activity;
static aqua_bootstrap_config_t bootstrap;
static bool motor_running;
static bool previous_start, previous_stop, previous_apply;

static uint16_t read_be16(const uint8_t *p) { return ((uint16_t)p[0] << 8) | p[1]; }
static void write_be16(uint8_t *p, uint16_t value) { p[0] = value >> 8; p[1] = value & 0xff; }
static bool coil_get(uint16_t m) { return (coils[m / 8] & (1u << (m % 8))) != 0; }
static void coil_set(uint16_t m, bool value)
{
    if (value) coils[m / 8] |= 1u << (m % 8);
    else coils[m / 8] &= ~(1u << (m % 8));
}

static bool is_writeable_m(uint16_t m)
{
    /* Request coils are writable by AquaOps. Local/fault bits are writable only in this simulator
     * so a commissioning laptop can emulate the physical selector and protection relay. */
    return m == M_APPLY_REQUEST || m == M_MANUAL_START || m == M_MANUAL_STOP ||
        m == M_AUTO_REQUEST || m == M_LOCAL_OVERRIDE || m == M_REMOTE_ALLOWED || m == M_FAULT;
}

static void wifi_event_handler(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    if (base == WIFI_EVENT && id == WIFI_EVENT_STA_START) esp_wifi_connect();
    else if (base == WIFI_EVENT && id == WIFI_EVENT_STA_DISCONNECTED) {
        xEventGroupClearBits(wifi_events, WIFI_CONNECTED_BIT);
        esp_wifi_connect();
    } else if (base == IP_EVENT && id == IP_EVENT_STA_GOT_IP) {
        xEventGroupSetBits(wifi_events, WIFI_CONNECTED_BIT);
    }
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
    snprintf((char *)config.sta.ssid, sizeof(config.sta.ssid), "%s", bootstrap.wifi_ssid);
    snprintf((char *)config.sta.password, sizeof(config.sta.password), "%s", bootstrap.wifi_password);
    ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));
    ESP_ERROR_CHECK(esp_wifi_set_config(WIFI_IF_STA, &config));
    ESP_ERROR_CHECK(esp_wifi_start());
}

static int recv_exact(int socket_fd, uint8_t *buffer, size_t length)
{
    size_t received = 0;
    while (received < length) {
        int result = recv(socket_fd, buffer + received, length - received, 0);
        if (result <= 0) return result;
        received += result;
    }
    return (int)received;
}

static size_t modbus_exception(uint8_t function, uint8_t code, uint8_t *response)
{
    response[0] = function | 0x80;
    response[1] = code;
    return 2;
}

static size_t process_pdu(const uint8_t *request, size_t length, uint8_t *response)
{
    if (length < 5) return modbus_exception(request[0], 0x03, response);
    const uint8_t function = request[0];
    const uint16_t address = read_be16(request + 1);
    const uint16_t value_or_count = read_be16(request + 3);

    xSemaphoreTake(register_lock, portMAX_DELAY);
    if (function == 0x01) {
        if (address < FX5_M_BASE || value_or_count == 0 || value_or_count > 64 ||
            (uint32_t)(address - FX5_M_BASE) + value_or_count > FX5_M_COUNT) {
            xSemaphoreGive(register_lock);
            return modbus_exception(function, 0x02, response);
        }
        const uint16_t first_m = address - FX5_M_BASE;
        const uint8_t byte_count = (value_or_count + 7) / 8;
        response[0] = function;
        response[1] = byte_count;
        memset(response + 2, 0, byte_count);
        for (uint16_t i = 0; i < value_or_count; ++i)
            if (coil_get(first_m + i)) response[2 + i / 8] |= 1u << (i % 8);
        xSemaphoreGive(register_lock);
        return 2 + byte_count;
    }

    if (function == 0x05) {
        if (address < FX5_M_BASE || address >= FX5_M_BASE + FX5_M_COUNT ||
            (value_or_count != 0x0000 && value_or_count != 0xff00)) {
            xSemaphoreGive(register_lock);
            return modbus_exception(function, 0x03, response);
        }
        const uint16_t m = address - FX5_M_BASE;
        if (!is_writeable_m(m)) {
            xSemaphoreGive(register_lock);
            return modbus_exception(function, 0x02, response);
        }
        coil_set(m, value_or_count == 0xff00);
        memcpy(response, request, 5); /* FC05 response echoes the address and written value. */
        xSemaphoreGive(register_lock);
        ESP_LOGI(TAG, "Write M%u=%s", m, value_or_count == 0xff00 ? "ON" : "OFF");
        return 5;
    }

    if (function == 0x03) {
        if (value_or_count == 0 || value_or_count > 32 || (uint32_t)address + value_or_count > D_REGISTER_COUNT) {
            xSemaphoreGive(register_lock);
            return modbus_exception(function, 0x02, response);
        }
        response[0] = function;
        response[1] = value_or_count * 2;
        for (uint16_t i = 0; i < value_or_count; ++i) write_be16(response + 2 + i * 2, holding[address + i]);
        xSemaphoreGive(register_lock);
        return 2 + value_or_count * 2;
    }

    if (function == 0x06) {
        if (address >= D_REGISTER_COUNT || (address != D_DESIRED_VERSION && address != D_ERROR_CODE)) {
            xSemaphoreGive(register_lock); return modbus_exception(function, 0x02, response);
        }
        holding[address] = value_or_count; memcpy(response, request, 5);
        xSemaphoreGive(register_lock); return 5;
    }

    xSemaphoreGive(register_lock);
    return modbus_exception(function, 0x01, response);
}

static void serve_client(int client_fd)
{
    uint8_t header[7], request[253], response[253], frame[260];
    while (recv_exact(client_fd, header, sizeof(header)) > 0) {
        const uint16_t protocol = read_be16(header + 2);
        const uint16_t mbap_length = read_be16(header + 4);
        if (protocol != 0 || header[6] != CONFIG_PLC_UNIT_ID || mbap_length < 2 || mbap_length > 254) break;
        const size_t pdu_length = mbap_length - 1; /* MBAP length includes Unit ID. */
        if (recv_exact(client_fd, request, pdu_length) <= 0) break;
        last_master_activity = xTaskGetTickCount();
        const size_t response_length = process_pdu(request, pdu_length, response);
        memcpy(frame, header, 4);
        write_be16(frame + 4, response_length + 1);
        frame[6] = header[6];
        memcpy(frame + 7, response, response_length);
        if (send(client_fd, frame, response_length + 7, 0) < 0) break;
    }
    close(client_fd);
}

static void modbus_server_task(void *arg)
{
    xEventGroupWaitBits(wifi_events, WIFI_CONNECTED_BIT, pdFALSE, pdTRUE, portMAX_DELAY);
    while (true) {
        int listen_fd = socket(AF_INET, SOCK_STREAM, IPPROTO_IP);
        int reuse = 1;
        setsockopt(listen_fd, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof(reuse));
        struct sockaddr_in address = { .sin_family = AF_INET,
            .sin_port = htons(CONFIG_PLC_MODBUS_PORT), .sin_addr.s_addr = htonl(INADDR_ANY) };
        if (bind(listen_fd, (struct sockaddr *)&address, sizeof(address)) != 0 || listen(listen_fd, 3) != 0) {
            ESP_LOGE(TAG, "Không mở được port %d: errno=%d", CONFIG_PLC_MODBUS_PORT, errno);
            close(listen_fd);
            vTaskDelay(pdMS_TO_TICKS(2000));
            continue;
        }
        ESP_LOGI(TAG, "FX5U-compatible Modbus TCP đang nghe port %d, Unit ID %d",
            CONFIG_PLC_MODBUS_PORT, CONFIG_PLC_UNIT_ID);
        while (true) {
            int client_fd = accept(listen_fd, NULL, NULL);
            if (client_fd < 0) break;
            serve_client(client_fd);
        }
        close(listen_fd);
    }
}

static void plc_scan_task(void *arg)
{
    last_master_activity = xTaskGetTickCount();
    TickType_t heartbeat_tick = last_master_activity;
    while (true) {
        xSemaphoreTake(register_lock, portMAX_DELAY);
        const bool watchdog_expired = (xTaskGetTickCount() - last_master_activity) >
            pdMS_TO_TICKS(CONFIG_PLC_WATCHDOG_SECONDS * 1000);
        const bool start = coil_get(M_MANUAL_START), stop = coil_get(M_MANUAL_STOP), apply = coil_get(M_APPLY_REQUEST);
        const bool local = coil_get(M_LOCAL_OVERRIDE), remote_allowed = coil_get(M_REMOTE_ALLOWED);
        if (apply && !previous_apply) {
            holding[D_APPLIED_VERSION] = holding[D_DESIRED_VERSION]; holding[D_ERROR_CODE] = 0;
            coil_set(M_APPLY_ACK, true); coil_set(M_CONFIG_ERROR, false); coil_set(M_CONFIG_REQUEST, false);
        }
        if (!apply) coil_set(M_APPLY_ACK, false);
        if (!local && remote_allowed && !watchdog_expired && !coil_get(M_FAULT)) {
            /* Rising-edge request semantics prevent a held/replayed command from retriggering. */
            if (stop && !previous_stop) motor_running = false;
            else if (start && !previous_start) motor_running = true;
        }
        if (coil_get(M_FAULT)) motor_running = false;
        coil_set(M_RUN_FEEDBACK, motor_running); coil_set(M_AUTO_FEEDBACK, coil_get(M_AUTO_REQUEST) && !local);
        previous_start = start; previous_stop = stop; previous_apply = apply;
        if (xTaskGetTickCount() - heartbeat_tick >= pdMS_TO_TICKS(1000)) {
            holding[D_HEARTBEAT_ADDRESS]++;
            heartbeat_tick = xTaskGetTickCount();
        }
        xSemaphoreGive(register_lock);
        vTaskDelay(pdMS_TO_TICKS(20));
    }
}

void app_main(void)
{
    esp_err_t init = nvs_flash_init();
    if (init == ESP_ERR_NVS_NO_FREE_PAGES || init == ESP_ERR_NVS_NEW_VERSION_FOUND) { ESP_ERROR_CHECK(nvs_flash_erase()); ESP_ERROR_CHECK(nvs_flash_init()); }
    aqua_provisioning_start_console(false);
    if (!aqua_provisioning_load(&bootstrap, false)) { ESP_LOGE(TAG, "Thiếu Wi-Fi/node bootstrap NVS; provision qua UART rồi restart."); while (true) vTaskDelay(pdMS_TO_TICKS(1000)); }
    wifi_events = xEventGroupCreate();
    register_lock = xSemaphoreCreateMutex();
    coil_set(M_REMOTE_ALLOWED, true); /* Physical program default for the commissioning simulator. */
    start_wifi();
    xTaskCreate(plc_scan_task, "plc_scan", 3072, NULL, 8, NULL);
    xTaskCreate(modbus_server_task, "modbus_tcp", 6144, NULL, 5, NULL);
}

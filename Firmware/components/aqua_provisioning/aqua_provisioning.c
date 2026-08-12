#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include "aqua_provisioning.h"
#include "esp_log.h"
#include "esp_system.h"
#include "nvs.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static const char *TAG = "aqua_provision";

static void read_value(nvs_handle_t nvs, const char *key, char *target, size_t size)
{
    size_t required = size;
    if (nvs_get_str(nvs, key, target, &required) != ESP_OK) target[0] = '\0';
}

bool aqua_provisioning_load(aqua_bootstrap_config_t *config, bool mqtt_required)
{
    memset(config, 0, sizeof(*config));
    nvs_handle_t nvs;
    if (nvs_open("aqua_boot", NVS_READONLY, &nvs) != ESP_OK) return false;
    read_value(nvs, "wifi_ssid", config->wifi_ssid, sizeof(config->wifi_ssid));
    read_value(nvs, "wifi_pass", config->wifi_password, sizeof(config->wifi_password));
    read_value(nvs, "mqtt_uri", config->mqtt_uri, sizeof(config->mqtt_uri));
    read_value(nvs, "mqtt_user", config->mqtt_username, sizeof(config->mqtt_username));
    read_value(nvs, "mqtt_pass", config->mqtt_password, sizeof(config->mqtt_password));
    read_value(nvs, "farm_id", config->farm_id, sizeof(config->farm_id));
    read_value(nvs, "pond_id", config->pond_id, sizeof(config->pond_id));
    read_value(nvs, "node_id", config->node_id, sizeof(config->node_id));
    nvs_close(nvs);
    return config->wifi_ssid[0] && config->node_id[0] &&
        (!mqtt_required || (config->mqtt_uri[0] && config->farm_id[0] && config->pond_id[0]));
}

static bool accepted_key(const char *key, bool mqtt_required)
{
    if (!strcmp(key, "wifi_ssid") || !strcmp(key, "wifi_pass") || !strcmp(key, "node_id")) return true;
    return mqtt_required && (!strcmp(key, "mqtt_uri") || !strcmp(key, "mqtt_user") ||
        !strcmp(key, "mqtt_pass") || !strcmp(key, "farm_id") || !strcmp(key, "pond_id"));
}

static void console_task(void *arg)
{
    const bool mqtt_required = (bool)(uintptr_t)arg;
    ESP_LOGW(TAG, "Provisioning UART: set <key> <value>, status, restart. Secret values are never echoed.");
    char line[256];
    while (fgets(line, sizeof(line), stdin)) {
        line[strcspn(line, "\r\n")] = 0;
        if (!strcmp(line, "restart")) { esp_restart(); }
        if (!strcmp(line, "status")) {
            aqua_bootstrap_config_t cfg;
            ESP_LOGI(TAG, "Bootstrap %s. Node=%s Farm=%s Pond=%s",
                aqua_provisioning_load(&cfg, mqtt_required) ? "READY" : "INCOMPLETE",
                cfg.node_id, cfg.farm_id, cfg.pond_id);
            continue;
        }
        char *command = strtok(line, " "); char *key = strtok(NULL, " "); char *value = strtok(NULL, "");
        if (!command || strcmp(command, "set") || !key || !value || !accepted_key(key, mqtt_required)) {
            ESP_LOGW(TAG, "Lệnh không hợp lệ. Dùng: set <key> <value>"); continue;
        }
        nvs_handle_t nvs;
        if (nvs_open("aqua_boot", NVS_READWRITE, &nvs) == ESP_OK) {
            ESP_ERROR_CHECK(nvs_set_str(nvs, key, value)); ESP_ERROR_CHECK(nvs_commit(nvs)); nvs_close(nvs);
            ESP_LOGI(TAG, "Đã lưu %s. Gõ status hoặc restart.", key);
        }
    }
    vTaskDelete(NULL);
}

void aqua_provisioning_start_console(bool mqtt_required)
{
    xTaskCreate(console_task, "aqua_provision", 4096, (void *)(uintptr_t)mqtt_required, 4, NULL);
}

#pragma once
#include <stdbool.h>
#include <stddef.h>

/* Bootstrap settings are stored in encrypted-capable NVS rather than compiled into firmware.
 * Production can replace the serial console with SoftAP/BLE while retaining this storage API. */
typedef struct {
    char wifi_ssid[33];
    char wifi_password[65];
    char mqtt_uri[129];
    char mqtt_username[65];
    char mqtt_password[65];
    char farm_id[49];
    char pond_id[49];
    char node_id[49];
} aqua_bootstrap_config_t;

bool aqua_provisioning_load(aqua_bootstrap_config_t *config, bool mqtt_required);
void aqua_provisioning_start_console(bool mqtt_required);

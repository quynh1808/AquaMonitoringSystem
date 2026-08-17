idf_component_register(SRCS "sensor_node.c"
    INCLUDE_DIRS "."
    # MQTT is a managed component on ESP-IDF 6. cJSON is declared by the
    # adjacent idf_component.yml and no longer needs the legacy "json" name.
    REQUIRES esp_wifi esp_event esp_netif nvs_flash mqtt lwip aqua_provisioning)

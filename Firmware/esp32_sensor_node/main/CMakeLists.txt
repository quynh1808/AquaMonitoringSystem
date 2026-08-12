idf_component_register(SRCS "sensor_node.c"
    INCLUDE_DIRS "."
    REQUIRES esp_wifi esp_event esp_netif nvs_flash mqtt json lwip aqua_provisioning)

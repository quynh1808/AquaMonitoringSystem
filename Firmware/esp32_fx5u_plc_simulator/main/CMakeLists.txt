idf_component_register(SRCS "fx5u_simulator.c"
    INCLUDE_DIRS "."
    REQUIRES esp_wifi esp_event esp_netif nvs_flash lwip aqua_provisioning)

# ESP32 bootstrap provisioning

Firmware không chứa SSID, mật khẩu, IP broker, FarmId, PondId hoặc NodeId cụ thể. Bootstrap được
lưu trong namespace NVS `aqua_boot`. Bản hiện tại dùng UART console; có thể thay UI bằng SoftAP/BLE
mà không đổi storage API trong `Firmware/components/aqua_provisioning`.

## Sensor node

Flash firmware, mở serial monitor rồi nhập từng lệnh:

```text
set wifi_ssid TEN_WIFI
set wifi_pass MAT_KHAU_WIFI
set mqtt_uri mqtt://192.168.100.66:1883
set mqtt_user aquaops_gateway
set mqtt_pass MAT_KHAU_MQTT
set farm_id farm_main
set pond_id pond_01
set node_id esp32-sensor-01
status
restart
```

## ESP32 PLC simulator

```text
set wifi_ssid TEN_WIFI
set wifi_pass MAT_KHAU_WIFI
set node_id fx5u-sim-01
status
restart
```

Console không echo giá trị secret. Khi triển khai thật nên bật NVS encryption và secure boot/flash
encryption theo chính sách thiết bị. Không commit `sdkconfig` chứa secret.

## Build ESP-IDF

```powershell
cd Firmware\esp32_sensor_node
idf.py set-target esp32
idf.py build
idf.py -p COM5 flash monitor
```

PLC simulator thực hiện tương tự trong `Firmware\esp32_fx5u_plc_simulator`.

# Firmware P03

- `esp32_sensor_node`: ESP32 WROOM-32E phát telemetry MQTT mỗi 30 giây.
- `esp32_fx5u_plc_simulator`: ESP32 WROOM-32E mô phỏng tập Modbus TCP cần thiết của Mitsubishi FX5U-32MT/D.

Mỗi project là một ESP-IDF application độc lập. Chọn cổng COM đúng của từng board và cấu hình Wi-Fi/secrets qua `idf.py menuconfig`; không commit file `sdkconfig` có mật khẩu.

Quy trình chuyển edge simulation sang thiết bị thật nằm trong `Docs/OPERATING-MODES.md`.

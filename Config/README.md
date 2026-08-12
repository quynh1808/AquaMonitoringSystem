# Cấu hình AquaOps P03

- `sensor-thresholds.json`: miền hợp lệ, warning/critical, hysteresis, delay và cooldown cho sáu metric.
- `sensor-connections.json`: MQTT là nguồn P03 mặc định; các adapter Modbus sensor cũ vẫn có thể bật khi lắp probe tương ứng.
- `device-control.json`: endpoint FX5U, timeout, heartbeat và map command/feedback/fault `M100–M302`.
- `system-behavior.json`: offline timeout 120 giây, retention, health score, calibration và automation.
- `seed-data.json`: chỉ một `pond_01`, ba actuator và không có reading/feed/vụ nuôi giả.
- `authentication.json`: bootstrap account development; không phải cơ chế đổi password user đã tồn tại.

Seed chỉ áp dụng khi collection `ponds` rỗng và không ghi đè MongoDB hiện có. Secret production phải được ghi đè bằng `.env`/environment, không lưu trong các file này.

Các biến quan trọng trên Ubuntu:

- `AQUAOPS_MODE`: `Simulation`, `EdgeSimulation` hoặc `Production`.
- `MQTT_BIND_ADDRESS`: IPv4 LAN của Ubuntu để ESP32 truy cập broker.
- `FX5U_HOST`, `FX5U_PORT`, `FX5U_UNIT_ID`: endpoint ESP32 simulator hoặc PLC thật.
- `HARDWARE_CONTROL_ENABLED`: mặc định false, chỉ bật sau commissioning.

Xem `Docs/OPERATING-MODES.md` trước khi chuyển profile.

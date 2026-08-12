# Cấu hình AquaOps

Cấu hình kết nối node không còn nằm trong thư mục này. MQTT broker, node socket, sensor channel và
PLC M/D mapping được lưu MongoDB, chỉnh trên giao diện Administrator. Cấu trúc cũ đã loại secret chỉ
được giữ tại `Docs/LEGACY-NODE-CONNECTION-REFERENCE.md` để tham khảo migration.

- `sensor-thresholds.json`: metadata hiển thị và alarm policy mặc định. Physical min/max thực tế lấy từ edge capabilities; policy từng ao chỉnh trên web.
- `system-behavior.json`: fallback offline timeout, retention, health score và calibration; AUTO policy nằm trong MongoDB.
- `seed-data.json`: chỉ một `pond_01`, không sensor/device/reading/feed/vụ nuôi giả.
- `authentication.json`: bootstrap account development; không phải cơ chế đổi password user đã tồn tại.

Seed chỉ áp dụng khi collection `ponds` rỗng và không ghi đè MongoDB hiện có. Secret production phải được ghi đè bằng `.env`/environment, không lưu trong các file này.

Các biến quan trọng trên Ubuntu:

- `AQUAOPS_MODE`: `Simulation`, `EdgeSimulation` hoặc `Production`.
- `MQTT_BIND_ADDRESS`: IPv4 LAN của Ubuntu để ESP32 truy cập broker.
- `HARDWARE_CONTROL_ENABLED`: mặc định false, chỉ bật sau commissioning.

Xem `Docs/OPERATING-MODES.md` trước khi chuyển profile.

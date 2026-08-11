# SensorSourceCoordinator

Background service khởi động các implementation của `ISensorDataSource`.

- Ghi trạng thái enabled/disabled của mọi adapter vào `data_source_health`.
- Chỉ chạy adapter được bật bởi `Config/sensor-connections.json`.
- Chạy đồng thời simulation, MQTT và nhiều kết nối Modbus khi cấu hình yêu cầu.

Khi thay phần cứng, chỉ cần bật nguồn tương ứng và cấu hình mapping; không sửa component Razor.

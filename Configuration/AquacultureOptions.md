# AquacultureOptions

Các lớp options ánh xạ file JSON sang cấu hình strongly typed.

## MongoDbOptions

Connection string, database name và timeout chọn server. Production truyền username/password/authentication database ở field riêng để mật khẩu có ký tự đặc biệt không bị ghép sai vào URI.

## SensorConnectionOptions

- `ActiveSources`.
- MQTT host/port/topic/TLS/reconnect.
- Nhiều kết nối Modbus TCP.
- Nhiều bus Modbus RTU.
- Register mapping: sensor, ao, metric, unit ID, địa chỉ, số register, kiểu dữ liệu, byte order, scale và offset.

## SensorThresholdCatalog

Mỗi metric có unit, decimal places, miền hợp lệ, warning/critical, hysteresis, delay, cooldown và nội dung xử lý.

## SimulationProfileOptions

Chu kỳ mô phỏng, seed, pin/RSSI/firmware, chính sách không cho dữ liệu giả ghi đè sensor thật và profile từng metric. `TargetCorrectionFactor` điều chỉnh tốc độ giá trị quay về xu hướng mục tiêu.

## SystemBehaviorOptions

Offline timeout, retention, giới hạn cache, chu kỳ nuôi mặc định, lịch hiệu chuẩn, health score, automation policies và template thiết bị.

Mọi options quan trọng được kiểm tra bởi `IValidateOptions` khi ứng dụng khởi động; cấu hình sai sẽ dừng sớm thay vì chạy với mapping nguy hiểm.

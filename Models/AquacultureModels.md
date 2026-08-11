# AquacultureModels

Chứa các document MongoDB và DTO dùng bởi UI.

## Document chính

- `Pond`: thông tin ao.
- `TelemetryData`: snapshot mới nhất theo ao.
- `SensorDefinition`: khai báo và sức khỏe sensor.
- `SensorReading`: bản ghi lịch sử append-only cùng metadata.
- `Device`, `DeviceCommand`: trạng thái và vòng đời lệnh.
- `AlertItem`, `AlarmRule`: sự kiện và rule cảnh báo.
- `CalibrationRecord`, `MaintenanceTask`: hiệu chuẩn và bảo trì.
- `AuditLog`: nhật ký thao tác.
- `DataSourceHealth`: sức khỏe adapter.
- `AppUser`: identity, PBKDF2 hash/salt, role, phạm vi ao, lockout và thời điểm đổi mật khẩu gần nhất.

## DTO UI

Các request thêm ao, thiết bị, thức ăn và mô phỏng được giữ tách khỏi document MongoDB để validation form không làm thay đổi schema lưu trữ.

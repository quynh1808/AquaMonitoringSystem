# MongoContext

`MongoContext` tạo một `MongoClient` dùng chung và cung cấp typed collection cho từng nhóm dữ liệu.

Các collection chính:

- `ponds`, `sensors`, `devices`: dữ liệu cấu hình.
- `sensor_readings`: lịch sử từng mẫu cảm biến.
- `telemetry_latest`: bản tổng hợp mới nhất của từng ao để Dashboard đọc nhanh.
- `alerts`, `alarm_rules`: cảnh báo và quy tắc.
- `device_commands`, `automation_logs`, `audit_logs`: truy vết thao tác.
- `calibration_records`, `maintenance_tasks`: hiệu chuẩn và bảo trì.

Không tạo `MongoClient` trong component Blazor. Mọi truy cập đi qua repository/service để dễ kiểm thử và thay đổi database.

Credential production được đặt riêng với connection endpoint. Cách này tránh lỗi URL-encoding khi password MongoDB chứa ký tự đặc biệt.

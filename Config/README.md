# Cấu hình AquaOps

## sensor-thresholds.json

Nguồn duy nhất cho miền hợp lệ và các mức cảnh báo. Không chép các số này vào Razor hoặc alert engine.

- `ValidMin/ValidMax`: đánh dấu dữ liệu không hợp lệ trước khi đưa vào automation.
- `WarningLow/WarningHigh`: vùng warning.
- `CriticalLow/CriticalHigh`: vùng critical.
- `Hysteresis`: khoảng phục hồi để tránh cảnh báo đóng/mở liên tục.
- `DelaySeconds`: thời gian bất thường phải duy trì trước khi mở cảnh báo.
- `CooldownSeconds`: hạn chế cập nhật/gửi lại quá dày.
- `DecimalPlaces`, `Unit`, thông báo và đề xuất xử lý.

Các giá trị mẫu phải được xác nhận theo loài, giai đoạn nuôi, thiết bị đo và quy trình kỹ thuật thực tế.

## simulation-profile.json

Điều khiển dữ liệu mô phỏng:

- `BaseValue`, `MinValue`, `MaxValue`.
- `MaxStep`, `TargetCorrectionFactor`.
- `DailyAmplitude`.
- xác suất và biên độ anomaly.
- offset riêng từng ao.
- pin, RSSI và firmware giả lập.
- `AllowSimulationOverrideRealSensors`: mặc định `false` để dữ liệu giả không ghi đè sensor đã chuyển sang MQTT/Modbus.

Tất cả có thể đổi mà không sửa C#.

## sensor-connections.json

Chọn nguồn và khai báo kết nối phần cứng:

- simulation;
- MQTT;
- nhiều kết nối Modbus TCP;
- nhiều bus Modbus RTU;
- register mapping, data type, byte order, scale và offset.

Khi chuyển từ mô phỏng sang phần cứng, giữ nguyên `SensorId`, `PondId` và `Metric`. Bản tin thật cho cùng `SensorId` sẽ tự thay nguồn của sensor; UI không cần thay đổi.

## system-behavior.json

Chứa:

- thời gian xác định offline và chu kỳ kiểm tra;
- retention dữ liệu;
- giới hạn runtime history;
- health score;
- vòng đời nuôi mặc định;
- chu kỳ hiệu chuẩn, số ngày nhắc trước và chu kỳ maintenance monitor;
- automation policy;
- template thiết bị.

## seed-data.json

Chỉ được dùng khi collection `ponds` còn trống. Sửa file này không ghi đè dữ liệu MongoDB đã tồn tại.


## Biến môi trường Docker

- `TZ`: múi giờ dùng cho hiển thị và nhịp ngày/đêm của mô phỏng.
- `AQUAOPS_MODE`: `Simulation` hoặc `Production`; Production được startup guard kiểm tra fail-fast.
- `ALERT_WEBHOOK_ENABLED` / `ALERT_WEBHOOK_URL`: delivery cảnh báo tới endpoint nội bộ.
- `MQTT_BIND_ADDRESS`: mặc định `127.0.0.1`; chỉ đổi sang IP LAN/`0.0.0.0` sau khi đã bật xác thực và ACL cho broker.
- `MONGO_*`: tài khoản root, tài khoản ứng dụng và database MongoDB.

Profile mặc định trong `compose.yaml` là Simulation. `compose.production.yaml` là overlay bất biến để tắt toàn bộ simulator và bật kết nối thật sau commissioning; không chỉnh tay nhiều biến rời rạc trên server.

## dashboard-simulation.json

Cung cấp các dữ liệu nghiệp vụ chưa có nguồn thật cho Dashboard tổng và sáu trang phân tích:

- Biomass, tăng trưởng, tỷ lệ sống, trọng lượng mẫu và FCR theo ao.
- Kế hoạch/thực tế thức ăn.
- Thời tiết và cảnh báo môi trường.
- Trạng thái server, MongoDB, MQTT, gateway và PLC.
- Công việc vận hành theo giờ tương đối của ngày hiện tại.
- Chuỗi lịch sử 7 ngày cho Biomass, thức ăn, năng lượng, cảnh báo và độ sẵn sàng cảm biến.
- Chu kỳ refresh, hệ số tăng trưởng, phụ tải và biên độ biến thiên.

`Tasks.DayOffset` xác định ngày tương đối so với ngày chạy; `Tasks.TimeLocal` dùng định dạng `HH:mm`. Có thể dùng `DueAtUtc` để đặt thời điểm tuyệt đối khi cần.

Các dữ liệu cảm biến nước không nằm trong file này mà tiếp tục dùng `simulation-profile.json` và pipeline chung. Xem `Config/dashboard-simulation.md`.

## device-control.json

Chứa transport mặc định, timeout ACK, cờ bật simulation và master safety switch cho actuator. Có thể ingest telemetry thật trong khi vẫn giữ `HardwareControlEnabled=false`.

Production secret không đặt trong thư mục này. Compose ghi đè bằng environment từ `.env`, file này đã bị Git ignore.

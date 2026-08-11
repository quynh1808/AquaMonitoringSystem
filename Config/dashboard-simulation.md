# dashboard-simulation.json

## Mục đích

Cung cấp dữ liệu mô phỏng cho các chỉ số chưa có nguồn phần cứng hoặc phân hệ nghiệp vụ thật:

- Biomass và tốc độ tăng trưởng.
- Tỷ lệ sống, trọng lượng mẫu và FCR.
- Kế hoạch/thực tế thức ăn.
- Điện năng lịch sử.
- Thời tiết và cảnh báo môi trường.
- Công việc vận hành.
- Trạng thái server, database, MQTT, gateway và PLC.

## Không chứa

File này không mô phỏng trực tiếp DO, pH, nhiệt độ nước, độ mặn, NH₃ hoặc mực nước. Các giá trị đó nằm trong `simulation-profile.json` và đi qua pipeline cảm biến chung.

## Cách thay bằng dữ liệu thật

Khi đã có các phân hệ thật, giữ nguyên model `DashboardSnapshot` và component Razor. Thay nguồn trong `DashboardAnalyticsService` bằng repository/provider tương ứng:

- Biomass: collection lấy mẫu và vụ nuôi.
- Thức ăn: kế hoạch + nhật ký cho ăn.
- Điện năng: điện kế/PLC.
- Thời tiết: trạm thời tiết hoặc API.
- Công việc: maintenance/task collection.
- Hạ tầng: health check/OpenTelemetry.

## Thời gian công việc

- `DayOffset=0`: hôm nay; `-1`: hôm qua; `1`: ngày mai.
- `TimeLocal`: giờ địa phương theo định dạng `HH:mm`.
- `DueAtUtc`: thời điểm tuyệt đối tùy chọn; khi có giá trị sẽ được ưu tiên.

Cách này giúp dữ liệu demo luôn phù hợp với ngày chạy thay vì bị cố định vào một ngày cũ.

## Reload cấu hình

File được nạp với `reloadOnChange`. Tham số hợp lệ sẽ được `IOptionsMonitor` cung cấp cho lần tạo snapshot tiếp theo. Các thay đổi làm sai validation sẽ khiến ứng dụng báo lỗi cấu hình thay vì âm thầm dùng dữ liệu không hợp lệ.

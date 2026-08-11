# DashboardSimulationOptions

## Mục đích

Ánh xạ file `Config/dashboard-simulation.json` thành các lớp cấu hình C#.
File này chỉ chứa những dữ liệu nghiệp vụ chưa có nguồn thật, ví dụ Biomass, kế hoạch thức ăn, thời tiết, công việc vận hành và lịch sử điện năng.

## Nguyên tắc

- Dữ liệu cảm biến DO, pH, nhiệt độ, độ mặn, NH₃ và mực nước không được tạo tại đây; chúng tiếp tục đi qua pipeline `Simulation/MQTT/Modbus`.
- Khi có phân hệ thật, có thể thay `DashboardAnalyticsService` bằng provider đọc dữ liệu thật mà không phải sửa component Dashboard.
- Không ghi các giá trị định mức trực tiếp trong Razor.

## Các nhóm cấu hình

- `Ponds`: dữ liệu Biomass, tăng trưởng, thức ăn, FCR và lần lấy mẫu theo từng ao.
- `Weather`: trạm thời tiết và cảnh báo môi trường.
- `Infrastructure`: trạng thái server, MongoDB, MQTT, gateway và PLC.
- `Tasks`: công việc vận hành trong ngày.
- `DailyHistory`: chuỗi dữ liệu phục vụ các biểu đồ tổng hợp.

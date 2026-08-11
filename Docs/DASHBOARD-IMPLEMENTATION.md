# Đối chiếu yêu cầu và triển khai Dashboard

## Phạm vi

Tài liệu này đối chiếu `Docs/DASHBOARD-REQUIREMENTS.md` với mã nguồn Dashboard đã triển khai.

## 1. Dashboard tổng

| Yêu cầu | Thành phần triển khai | Nguồn dữ liệu |
|---|---|---|
| Tổng số ao | KPI `AO NUÔI ĐANG QUẢN LÝ` | MongoDB/runtime `Ponds` |
| Số vụ đang hoạt động, ao sắp thu hoạch | KPI ao nuôi và trang `/analytics/ponds` | Runtime + ngày thả/thu hoạch |
| Tuổi vụ | KPI tuổi vụ trung vị, khoảng min–max | Tính từ ngày thả |
| Sinh khối ước tính | KPI Biomass, trang `/analytics/growth` | `dashboard-simulation.json` cho đến khi có dữ liệu lấy mẫu thật |
| Thiết bị và trạng thái | KPI thiết bị, trang `/analytics/equipment` | Runtime/MongoDB |
| Công suất và điện năng | KPI năng lượng, biểu đồ 7 ngày | Runtime + file mô phỏng nghiệp vụ |
| Cảnh báo | KPI cảnh báo, trang `/analytics/risks` | Runtime/MongoDB |
| Chất lượng nước | KPI DO có trung bình, thấp nhất và số ao dưới ngưỡng | Pipeline cảm biến |
| Thức ăn | KPI kế hoạch/thực tế/từ đầu vụ | File mô phỏng nghiệp vụ + log hiện có |
| Thời tiết | Dải vận hành và panel thời tiết | File mô phỏng; thay bằng provider thật sau này |
| Chất lượng dữ liệu | Dải vận hành, trang rủi ro | Sensor registry/runtime |
| Công việc hôm nay | Dải vận hành và Action Center | File mô phỏng; thời gian tương đối theo ngày hiện tại |
| Hạ tầng | Dải vận hành và System Health | File mô phỏng + `DataSourceHealth` runtime |

## 2. Phân tích tổng thể

Menu mở rộng/thu gọn gồm sáu miền:

1. `/analytics/ponds` — ao nuôi và vụ nuôi.
2. `/analytics/growth` — Biomass và tăng trưởng.
3. `/analytics/water-quality` — chất lượng nước.
4. `/analytics/feed` — thức ăn.
5. `/analytics/equipment` — thiết bị và năng lượng.
6. `/analytics/risks` — cảnh báo, dữ liệu và hạ tầng.

Các KPI trên Dashboard điều hướng đến đúng miền; không tạo quá nhiều menu cấp cao cho từng con số nhỏ.

## 3. Analytics trên Dashboard

- Heatmap chất lượng nước theo ao và metric.
- Phân bố giai đoạn nuôi.
- Phân bố đối tượng nuôi.
- Biomass 7 ngày.
- Điện năng 7 ngày.
- Cảnh báo 7 ngày, tách warning và critical.
- Độ sẵn sàng cảm biến 7 ngày.

## 4. Live Monitoring

Mỗi ao là một card có:

- DO, pH, nhiệt độ và NH₃ hiển thị lớn.
- Biomass, thức ăn hôm nay, mật độ và ngày thu hoạch dự kiến.
- Thiết bị đang chạy, cảm biến online và số cảnh báo.
- Tỷ lệ sức khỏe, nguồn dữ liệu thật/mô phỏng và thời gian cập nhật cuối.
- Bộ lọc theo tên, đối tượng nuôi, trạng thái và ao cần chú ý.

Dashboard không đặt nút bật/tắt thiết bị trực tiếp. Điều khiển chỉ thực hiện trong workspace riêng của ao.

## 5. File cấu hình mô phỏng

`Config/dashboard-simulation.json` chứa các dữ liệu chưa có nguồn thật:

- Biomass, tăng trưởng, tỷ lệ sống, trọng lượng mẫu và FCR.
- Kế hoạch/thực tế thức ăn.
- Thời tiết.
- Trạng thái hạ tầng.
- Công việc vận hành.
- Chuỗi lịch sử 7 ngày.
- Các hệ số mặc định và biên độ biến thiên.

File dùng `reloadOnChange`; thay đổi hợp lệ được đọc lại mà không sửa Razor.

## 6. Đường chuyển sang dữ liệu thật

Các component chỉ nhận `DashboardSnapshot`. Khi có phần cứng hoặc phân hệ nghiệp vụ thật:

- Cảm biến tiếp tục đi qua Simulation/MQTT/Modbus pipeline hiện có.
- Biomass đọc từ collection lấy mẫu/vụ nuôi.
- Thức ăn đọc từ kế hoạch và nhật ký cho ăn.
- Điện năng đọc từ điện kế/PLC.
- Thời tiết đọc từ trạm tại trại hoặc dịch vụ dự báo.
- Công việc đọc từ maintenance/task collection.
- Hạ tầng đọc từ health check/OpenTelemetry.

Chỉ cần thay provider bên trong `DashboardAnalyticsService`; URL, view model và component Dashboard giữ nguyên.

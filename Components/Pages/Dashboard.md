# Dashboard tổng

## Mục đích

Dashboard là trung tâm điều hành toàn trại, không phải trang báo cáo chi tiết. Người vận hành phải nhìn thấy ngay:

1. Tình trạng hiện tại.
2. Vấn đề cần xử lý.
3. Công việc tiếp theo.

## Cấu trúc

### KPI có thể nhấn

- Ao nuôi đang quản lý.
- Tuổi vụ trung vị.
- Sinh khối ước tính.
- Cảnh báo đang hoạt động.
- Chất lượng nước toàn trại.
- Thiết bị và vận hành.
- Năng lượng và phụ tải.
- Thức ăn hôm nay.

Mỗi card chuyển tới đúng miền trong `Phân tích tổng thể`.

### Dải vận hành

Hiển thị chất lượng dữ liệu, công việc hôm nay, hạ tầng và thời tiết.

### Analytics

- Heatmap chất lượng nước.
- Phân bố giai đoạn và đối tượng nuôi.
- Biomass 7 ngày.
- Điện năng 7 ngày.
- Cảnh báo 7 ngày.
- Độ sẵn sàng cảm biến.

### Live Monitoring

Mỗi ao là một card có DO, pH, nhiệt độ, NH₃, Biomass, thức ăn, mật độ, ngày thu hoạch, thiết bị, cảm biến, cảnh báo, nguồn dữ liệu và thời gian cập nhật.

## Nguồn dữ liệu

- Cảm biến: runtime từ Simulation/MQTT/Modbus.
- Biomass, thời tiết, công việc, điện năng lịch sử: `Config/dashboard-simulation.json` cho đến khi có nguồn thật.

## Nguyên tắc an toàn

Dashboard tổng không chứa nút điều khiển thiết bị trực tiếp. Người dùng phải mở workspace của ao để điều khiển, tránh thao tác nhầm.

## Tham số không ghi trực tiếp trong UI

- Chu kỳ cập nhật lấy từ `DashboardSimulation:RefreshSeconds`.
- Mốc điểm sức khỏe lấy từ `SystemBehavior:HealthScore`.
- Ngưỡng metric lấy từ `sensor-thresholds.json`.
- Biomass, thức ăn, thời tiết, năng lượng lịch sử và công việc lấy từ `dashboard-simulation.json` cho đến khi có provider thật.

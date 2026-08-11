# Dashboard change log

## Bản cập nhật 26/07/2026

### Thêm mới

- Dashboard KPI có điều hướng.
- Sáu trang phân tích tổng thể.
- Heatmap chất lượng nước.
- Biểu đồ Biomass, thức ăn, năng lượng, cảnh báo và sensor availability.
- Live Monitoring card riêng cho từng ao.
- Bộ lọc ao theo tên, đối tượng nuôi, trạng thái và mức chú ý.
- Panel công việc, hạ tầng và thời tiết.
- File `Config/dashboard-simulation.json`.
- `DashboardAnalyticsService` và các view model tổng hợp.
- Menu mở rộng/thu gọn cho phân tích và danh sách ao.

### Điều chỉnh nghiệp vụ

- Đổi “sản lượng” thành “sinh khối ước tính”.
- Tuổi vụ dùng trung vị và khoảng min–max.
- DO trung bình luôn kèm DO thấp nhất, số ao dưới ngưỡng và số ao có dữ liệu hợp lệ.
- Tách công suất kW khỏi điện năng kWh.
- Thức ăn luôn có phạm vi hôm nay/kế hoạch/từ đầu vụ.
- Hiển thị rõ nguồn Simulation/MQTT/Modbus và thời gian cập nhật.
- Không đặt điều khiển thiết bị trực tiếp trên Dashboard tổng.

### Cấu hình hóa

- Ngưỡng cảm biến: `sensor-thresholds.json`.
- Mốc health score: `system-behavior.json`.
- Chu kỳ refresh, Biomass, FCR, thức ăn, thời tiết, năng lượng, task và hạ tầng: `dashboard-simulation.json`.

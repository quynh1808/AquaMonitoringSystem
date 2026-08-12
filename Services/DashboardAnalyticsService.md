# DashboardAnalyticsService

Snapshot bắt buộc nhận danh sách PondId mà principal được phép xem. Khi DashboardSimulation tắt, KPI biomass/tăng trưởng lấy từ ProductionCycle đã được tính lại; weather, history, task và infrastructure giả không được tạo.

## Vai trò

Tổng hợp một `DashboardSnapshot` từ hai nhóm nguồn:

1. **Dữ liệu vận hành hiện tại** trong `FarmRuntimeState`: ao, cảm biến, telemetry, thiết bị, cảnh báo và tình trạng nguồn dữ liệu.
2. **Dữ liệu nghiệp vụ chưa có phần cứng/phân hệ thật** trong `Config/dashboard-simulation.json`: Biomass, FCR, kế hoạch thức ăn, thời tiết, công việc và lịch sử điện năng.

## Tại sao tách thành service?

Các component Razor không đọc trực tiếp file JSON và không tự tính KPI. Khi có nguồn dữ liệu thật, có thể thay logic trong service hoặc tạo provider mới mà không phải thiết kế lại Dashboard.

## Quy tắc quan trọng

- DO trung bình luôn đi kèm DO thấp nhất và số ao dưới ngưỡng.
- Biomass có thời điểm lấy mẫu và mức độ tin cậy.
- Công suất `kW` được tách khỏi điện năng `kWh`.
- Mỗi card ao cho biết số cảm biến thực/mô phỏng và thời gian cập nhật cuối.
- Heatmap sử dụng các ngưỡng trong `sensor-thresholds.json`, không ghi ngưỡng trực tiếp trong code UI.

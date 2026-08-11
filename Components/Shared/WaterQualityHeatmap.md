# WaterQualityHeatmap

Heatmap so sánh chất lượng nước giữa các ao.

## Trạng thái màu

- `normal`: trong vùng theo dõi.
- `warning`: vượt ngưỡng cảnh báo.
- `critical`: vượt ngưỡng nghiêm trọng.
- `no-data`: cảm biến offline hoặc dữ liệu không hợp lệ.

Trạng thái được tính trong `DashboardAnalyticsService` dựa trên `sensor-thresholds.json`.

# DashboardModels

Các model trong file này là **view model tổng hợp**, không phải collection MongoDB độc lập.

## Luồng dữ liệu

`MongoDB/runtime + dashboard-simulation.json -> DashboardAnalyticsService -> DashboardSnapshot -> Razor components`

## Thành phần chính

- `DashboardSnapshot`: toàn bộ dữ liệu một lần render Dashboard.
- `DashboardPondSummary`: dữ liệu card Live Monitoring của từng ao.
- `DashboardHeatmapRow`: trạng thái từng thông số nước theo ao.
- `DashboardSeriesPoint`: dữ liệu biểu đồ lịch sử.
- `DashboardTaskItem`: công việc vận hành trong ngày.
- `DashboardInfrastructureStatus`: tình trạng server, database, broker, gateway và PLC.

Các model này giúp UI không phụ thuộc vào chi tiết nguồn dữ liệu. Khi chuyển từ mô phỏng sang dữ liệu thật, chỉ cần thay provider/service tạo snapshot.

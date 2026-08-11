# PondDetail workspace

Workspace giữ route riêng cho từng ao và các tab Tổng quan, Cảm biến, Thiết bị, Cho ăn và Cảnh báo.

Các thay đổi trong phiên bản MongoDB:

- `SensorMetricCard` đọc ngưỡng từ JSON thay vì số viết trực tiếp trong Razor.
- Trend lấy lịch sử đã ingest.
- Mô phỏng thủ công phát `SensorEnvelope`, không sửa state trực tiếp.
- Điểm sức khỏe dùng `FarmStateService.CalculateHealthScore` dựa trên cấu hình và tình trạng dữ liệu.
- Thời gian UTC trong database được đổi sang giờ local khi hiển thị.

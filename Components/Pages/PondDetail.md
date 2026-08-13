# PondDetail workspace

Workspace giữ route riêng cho từng ao và các tab Tổng quan, Cảm biến, Thiết bị, Cho ăn và Cảnh báo.

Các thay đổi trong phiên bản MongoDB:

- `SensorMetricCard` chỉ được dựng từ sensor Administrator đã thêm; ao mới không có card mặc định.
- Trend lấy lịch sử đã ingest.
- Mô phỏng thủ công phát `SensorEnvelope`, không sửa state trực tiếp.
- Điểm sức khỏe dùng đúng tập metric đã khai báo, không chấm ngầm sáu metric cố định.
- Thời gian UTC trong database được đổi sang giờ local khi hiển thị.
- Xóa sensor/thiết bị là decommission có xác nhận và ACK; lịch sử được giữ.

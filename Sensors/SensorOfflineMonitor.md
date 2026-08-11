# SensorOfflineMonitor

Worker định kỳ so sánh `LastSeenAtUtc` của từng cảm biến với `SensorOfflineAfterSeconds` trong `Config/system-behavior.json`.

Khi quá hạn:

- cảm biến chuyển sang `offline`;
- chất lượng mẫu chuyển thành `no-data`;
- tạo hoặc cập nhật một cảnh báo connectivity riêng cho cảm biến;
- giữ lại giá trị cuối để điều tra, nhưng UI không coi giá trị cũ là dữ liệu đang hoạt động.

Worker đồng thời tổng hợp trạng thái ở cấp ao:

- không còn cảm biến online: `IsOnline=false`, `Quality=no-data`;
- chỉ một phần cảm biến online: `IsOnline=true`, `Quality=partial`;
- tất cả cảm biến trở lại online: trạng thái `partial/no-data` được trả về `good`.

Khi `SensorIngestionWorker` nhận mẫu mới, cảm biến trở lại online và cảnh báo connectivity được đóng tự động.

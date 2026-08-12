# AlertEngineService

Engine duy trì occurrence count và vòng đời new/acknowledged/resolved. Cảnh báo mới hoặc mức critical được chuyển cho `AlertNotificationService`; lỗi webhook được lưu delivery log nhưng không làm dừng sensor ingestion.

Engine đánh giá cảnh báo theo `Config/sensor-thresholds.json`.

Cơ chế chống cảnh báo giả:

- `DelaySeconds`: giá trị phải vi phạm liên tục đủ lâu.
- `Hysteresis`: phải quay sâu vào vùng an toàn mới đóng cảnh báo.
- `CooldownSeconds`: giới hạn tần suất cập nhật cảnh báo.
- Miền `ValidMin/ValidMax`: tách lỗi dữ liệu/cảm biến khỏi cảnh báo môi trường.

Cảnh báo đang hoạt động được upsert thay vì tạo hàng nghìn bản ghi giống nhau. Khi hồi phục, trạng thái chuyển sang `resolved` và vẫn giữ lịch sử trong MongoDB.

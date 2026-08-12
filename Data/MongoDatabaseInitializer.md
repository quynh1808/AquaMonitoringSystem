# MongoDatabaseInitializer

Module này chạy trước khi web bắt đầu nhận dữ liệu.

Chức năng:

- Ping MongoDB để fail-fast khi cấu hình sai.
- Tạo index truy vấn theo ao, metric và thời gian.
- Tạo TTL index để tự xóa lịch sử quá thời hạn `ReadingRetentionDays`; nếu giá trị retention đổi, index được kiểm tra và tạo lại an toàn ở lần khởi động kế tiếp.
- Khi database trống, đọc `Config/seed-data.json`, `sensor-thresholds.json` và `simulation-profile.json` để tạo ao, thiết bị, cảm biến và giá trị đầu tiên.

Dữ liệu mẫu không nằm trong code C#. Muốn thay số ao, thiết bị hoặc giá trị khởi tạo, sửa file JSON rồi dùng database mới hoặc xóa volume MongoDB để seed lại.

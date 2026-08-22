# `PostgreSqlRetentionWorker`

## Mục đích

Worker thay MongoDB TTL cũ bằng chính sách retention rõ trong code/config. Mỗi 24 giờ, worker xóa:

- `sensor_readings` cũ hơn `ReadingRetentionDays` (mặc định 365 ngày).
- connection log, sensor health event và quarantined message quá `ExpiresAtUtc`.

Audit log, chu kỳ, kho, chi phí và hồ sơ sản xuất không bị worker này xóa vì thuộc traceability.

## Failure mode

Lỗi retention được log và retry ở chu kỳ sau; không làm host dừng vì giữ dữ liệu lâu hơn an toàn hơn
xóa nhầm. Khi volume lớn, phải đo thời gian/lock và chuyển sang batch hoặc partition trước khi thay
đổi production.

**Retention** là thời gian lưu; khác backup. Dữ liệu hết retention bị xóa khỏi database online,
nhưng backup cũ vẫn có vòng đời riêng theo chính sách vận hành.

Liên quan: `SystemBehaviorOptions`, `AquaDbContext`, `Program.cs` và tài liệu PostgreSQL mục 11.

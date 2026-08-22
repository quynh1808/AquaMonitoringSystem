# `PostgreSqlModelTests`

Nhóm test này kiểm tra model metadata mà không yêu cầu PostgreSQL đang chạy:

- `LatestSensorState` tồn tại và `TelemetryData` cố định không được persist.
- Sensor routing metadata được flatten thành cột.
- EventId có unique index.
- Sensor, device, production cycle và reading có foreign key tới pond.

Đây là structural test, không thay integration test PostgreSQL thật. Trước production vẫn phải chạy
migration trên database tạm, ingest duplicate/backfill và diễn tập transaction/restore.

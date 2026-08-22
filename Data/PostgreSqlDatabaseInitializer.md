# `PostgreSqlDatabaseInitializer`

## Mục đích và thứ tự startup

Initializer chuẩn bị system of record trước khi MQTT/Modbus worker chạy:

1. `MigrateAsync` áp dụng migration còn thiếu khi `AutoMigrate=true`.
2. Mở transaction khởi tạo.
3. Nếu bảng `ponds` rỗng, đọc `Config/seed-data.json` và tạo đúng một ao pilot.
4. Đồng bộ default alarm rule từ `sensor-thresholds.json`.
5. Commit và ghi structured log.

Baseline hiện không tạo sensor, device, reading, feed hoặc telemetry giả. Không có ETL MongoDB và
không chạy logic backfill tài liệu cũ.

## Vì sao seed và migration tách nhau

Migration định nghĩa **cấu trúc** có phiên bản; seed định nghĩa baseline nghiệp vụ có điều kiện.
Trộn dữ liệu vận hành vào migration làm rollback/nâng cấp khó kiểm soát. Alarm rule được sync theo
ID ổn định `default:<metric>` nên chạy lặp không tạo bản sao.

## Failure mode

- Credential sai/mất database: startup fail, readiness không xanh và worker chưa nhận message.
- Migration lỗi: transaction schema do EF/PostgreSQL quản lý; không tiếp tục seed.
- Seed JSON lỗi: startup fail rõ thay vì tạo trạng thái nửa đúng.

## Quan hệ

Được gọi bởi `PostgreSqlBootstrapHostedService`; dùng `AquaDbContext`, `PostgreSqlOptions`,
`SensorThresholdCatalog` và `Config/seed-data.json`. Schema nằm trong `Data/Migrations/`.

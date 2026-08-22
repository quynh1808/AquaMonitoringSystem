# `PostgreSqlBootstrapHostedService`

Hosted service này bảo đảm thứ tự khởi động:

1. Apply PostgreSQL migrations và baseline.
2. Tạo admin bootstrap nếu bảng user rỗng.
3. Tính lại metric chu kỳ dẫn xuất.
4. Đọc snapshot bền vững và khởi tạo `FarmRuntimeState`.
5. Chỉ sau đó các hosted worker đăng ký phía sau mới xử lý MQTT/health/PLC.

`FarmRuntimeState` là projection thread-safe giúp UI đọc nhanh; PostgreSQL vẫn là system of record.
Nếu bootstrap fail, host không được coi là ready. Log không in password hoặc connection string.

Liên quan trực tiếp: `PostgreSqlDatabaseInitializer`, `UserAccountService`,
`ProductionCycleService`, `FarmRepository` và `FarmRuntimeState`.

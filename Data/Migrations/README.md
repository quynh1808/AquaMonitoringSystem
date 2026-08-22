# EF Core migrations

Thư mục này là lịch sử schema PostgreSQL có phiên bản. `InitialPostgreSql` tạo toàn bộ table,
constraint, JSONB column, partial unique index, BRIN và foreign key cho clean database.

Không sửa/xóa migration đã được áp dụng production. Khi model đổi:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add TenMigration --output-dir Data/Migrations
dotnet tool run dotnet-ef migrations script --idempotent
```

Review cả `Up`, `Down` và model snapshot. Migration phá schema phải theo expand/contract để image
trước vẫn rollback được. Tool version được khóa trong `dotnet-tools.json`.

Thuật ngữ: **migration history** là bảng `__EFMigrationsHistory`; **model snapshot** là model EF sau
migration cuối; **idempotent script** chỉ áp dụng migration chưa có trong history.

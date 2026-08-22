# `AquaDbContextFactory` và `AquaDbConnection`

## Mục đích

`AquaDbConnection.Build` tạo Npgsql connection string từ các trường cấu hình rời. Cách này tránh
ghép password vào URI/chuỗi thủ công, đặc biệt khi password có dấu chấm phẩy hoặc ký tự đặc biệt.

`AquaDbContextDesignFactory` cho `dotnet-ef` dựng model trước khi Windows/Linux đã có PostgreSQL.
Credential `design-time-only` không được dùng để kết nối; migration generation chỉ cần metadata.

## Vai trò

- Runtime: `Program.cs` bind `PostgreSqlOptions`, tạo pooled DbContext factory.
- Design time: local tool `dotnet-ef` tìm factory và tạo migration.
- Bảo trì: developer không phải đặt production secret để review schema.

## Thuật ngữ

- **Connection pooling**: tái sử dụng kết nối vật lý; DbContext vẫn được tạo/dispose theo operation.
- **Design-time factory**: điểm tạo context cho tooling, không phải service runtime.
- **Npgsql**: driver/provider .NET chính cho PostgreSQL.

Xem `Docs/16.POSTGRESQL-SETUP-WINDOWS-UBUNTU.md` để cấu hình và chạy migration.

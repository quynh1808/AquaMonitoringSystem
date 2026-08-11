# Báo cáo kiểm tra bản sửa UI

## Đã kiểm tra tĩnh

- `Program.cs` không còn tên kiểu `AuthenticationOptions` mơ hồ.
- Global authorization fallback đã được loại bỏ.
- Mọi page nghiệp vụ đều có `[Authorize]` hoặc role cụ thể.
- Các page công khai có `[AllowAnonymous]`.
- `App.razor` tham chiếu static assets qua `@Assets[...]`.
- `UseStaticFiles()` đứng trước authentication middleware.
- CSS cân bằng dấu ngoặc.
- JSON cấu hình đọc được.
- XML project đọc được.
- Mỗi file `.cs` và `.razor` đều có file `.md` tương ứng.

## Cần xác nhận trên máy có .NET/Docker

```powershell
dotnet restore
dotnet build
dotnet run
```

Sau đó kiểm tra Network trong trình duyệt:

```text
/app.css                    200 text/css
/app.js                     200 javascript
/_framework/blazor.web.js   200 javascript
/login                      200 text/html
```

## Trường hợp Docker cache phiên bản cũ

```powershell
docker compose down
docker compose build --no-cache aquaops
docker compose up -d
```

Sau đó hard refresh trình duyệt bằng `Ctrl+F5`.

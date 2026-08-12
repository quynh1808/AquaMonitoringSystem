# Program.cs

## Vai trò

Điểm khởi động, dependency-injection root, authentication pipeline và endpoint mapping của ứng dụng.

## Cấu hình được nạp

- `sensor-thresholds.json`
- `simulation-profile.json`
- `sensor-connections.json`
- `system-behavior.json`
- `dashboard-simulation.json`
- `authentication.json`
- `device-control.json`
- `production-cycle-simulation.json`

## Authentication và authorization

- Cookie authentication tên `AquaOps.Auth`.
- `DefaultPolicy` yêu cầu người dùng đã đăng nhập cho các trang khai báo `[Authorize]`.
- Không dùng global `FallbackPolicy`, vì fallback có thể vô tình bảo vệ static assets và endpoint bootstrap Blazor trên trang anonymous.
- Policy role:
  - `ManageUsers`
  - `ManageFarm`
  - `OperatePonds`
- Endpoint `/auth/login` xác thực tài khoản MongoDB và tạo cookie.
- Endpoint `/auth/logout` xóa cookie.
- Hai form dùng antiforgery token; login có fixed-window rate limit theo địa chỉ IP.
- `FarmAccessService` cưỡng chế role/phạm vi ao trong service, không phụ thuộc việc nút có ẩn trên UI hay không.
- Chế độ CLI `reset-admin-password` chỉ đọc password từ environment ngắn hạn, chỉ reset role Administrator rồi thoát trước khi web host khởi động.

## Production middleware

- Ứng dụng phục vụ HTTP trực tiếp trên cổng LAN do Compose publish; không dùng forwarded headers, HTTPS redirect hoặc HSTS.
- Data Protection keys được persist vào `/var/lib/aquaops/keys`. Compose chạy `data-protection-init` một lần để đặt volume về UID/GID 1654 trước khi AquaOps non-root khởi động.
- `/health/live` kiểm process, `/health/ready` và `/health` kiểm MongoDB.

## Static assets

- `UseStaticFiles()` chạy trước authentication/authorization.
- `MapStaticAssets().AllowAnonymous()` công bố asset pipeline của .NET 10.
- `App.razor` dùng `@Assets[...]` cho CSS, JavaScript và favicon.

Cấu trúc này bảo đảm `/` và `/login` luôn tải được giao diện trước khi có cookie.

## Dịch vụ nền

- Khởi tạo MongoDB và runtime state.
- Mongo write queue.
- Sensor ingestion.
- Simulation/MQTT/Modbus source coordinator.
- Sensor offline monitor.
- Maintenance/calibration monitor.
- Production-cycle simulation worker.

## Endpoint

- `/health/live`, `/health/ready`, `/health`
- `/auth/login`
- `/auth/logout`
- Blazor Interactive Server routes

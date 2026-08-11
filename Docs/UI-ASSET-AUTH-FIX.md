# Khắc phục trang chủ và trang đăng nhập mất giao diện

## Hiện tượng

Trang `/` và `/login` vẫn trả nội dung HTML nhưng không tải đúng CSS/JavaScript, vì vậy trình duyệt chỉ hiển thị nền trắng hoặc nội dung không định dạng.

## Nguyên nhân

Phiên bản trước sử dụng `FallbackPolicy` yêu cầu đăng nhập cho mọi endpoint không có metadata riêng. Chính sách này có thể áp dụng cho cả static assets và endpoint bootstrap của Blazor, trong khi người dùng tại `/` và `/login` chưa có cookie đăng nhập.

Các tài nguyên có thể bị chặn gồm:

- `app.css`
- `app.js`
- `favicon.svg`
- các tài nguyên `_framework`

Ngoài ra, `Program.cs` cùng lúc import hai kiểu có tên `AuthenticationOptions`, tạo nguy cơ lỗi tên kiểu không rõ ràng khi compile.

## Thay đổi

1. Bỏ global `FallbackPolicy`.
2. Dùng `DefaultPolicy` cho những trang khai báo `[Authorize]`.
3. Bổ sung `[Authorize]` trực tiếp cho các trang vận hành.
4. Cho static assets đi qua `UseStaticFiles()` trước authentication middleware.
5. Đánh dấu `MapStaticAssets()` là anonymous.
6. Dùng `@Assets[...]` trong `App.razor` để lấy đường dẫn static asset đúng với asset pipeline của .NET 10.
7. Đổi tên tham chiếu cấu hình thành alias `AquaAuthenticationOptions` để tránh xung đột kiểu.
8. Thiết kế lại trang đăng nhập theo bố cục hai cột, responsive trên mobile.

## Nguyên tắc phân quyền sau sửa

- `/`, `/login`, `/access-denied`, `/Error`: anonymous.
- Các trang Dashboard, Analytics, ao, cảm biến, thiết bị, thức ăn, cảnh báo và cố vấn: `[Authorize]`.
- Quản lý người dùng: role `Administrator`.
- Quản lý vụ nuôi: người dùng đã đăng nhập; thao tác chỉnh sửa tiếp tục giới hạn bằng `AuthorizeView` và service policy.

## Kiểm tra trên trình duyệt

Mở Developer Tools → Network và xác nhận:

- `/app.css` trả HTTP 200 và `content-type: text/css`.
- `/_framework/blazor.web.js` trả HTTP 200.
- `/login` không tạo redirect loop.
- Sau khi đăng nhập, `/dashboard` hiển thị MainLayout.

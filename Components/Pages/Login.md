# Login.razor

Trang đăng nhập công khai của AquaOps.

## Giao diện

- Bố cục hai cột trên desktop.
- Cột trái giới thiệu trung tâm điều hành, pipeline dữ liệu và phân quyền.
- Cột phải chứa form đăng nhập.
- Tự chuyển thành một cột trên tablet/mobile.

## Luồng xử lý

Form POST tới `/auth/login` với:

- `username`
- `password`
- `returnUrl`

Endpoint kiểm tra tài khoản trong MongoDB, tạo cookie xác thực khi thành công và redirect tới `returnUrl` an toàn.
Form có antiforgery token; endpoint có fixed-window rate limit theo IP trước khi truy vấn tài khoản.
Trường mật khẩu có nút `Hiện/Ẩn` chạy bằng helper DOM tối thiểu để hoạt động ngay cả trước khi Blazor circuit kết nối; helper không đọc nội dung mật khẩu.

## Trạng thái lỗi

- `error=invalid`: sai tài khoản hoặc mật khẩu.
- `error=locked`: tài khoản đang tạm khóa.

Trang có `[AllowAnonymous]` và dùng `LandingLayout`.

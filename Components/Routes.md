# Routes.razor

Router trung tâm của ứng dụng.

## Cách xử lý

- `AuthorizeRouteView` đọc `[Authorize]`, role và policy trên từng page.
- Trang đã đăng nhập dùng `MainLayout` theo mặc định.
- Khi chưa đăng nhập vào route được bảo vệ, hiển thị thông báo bằng `LandingLayout`, tránh render sidebar nghiệp vụ.
- Route không tồn tại hiển thị trang 404.
- `FocusOnNavigate` đưa focus về tiêu đề `h1` sau điều hướng.

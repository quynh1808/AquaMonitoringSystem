# Triển khai ngày 26/07/2026

## Dashboard
- Live Monitoring được chuyển lên ngay sau KPI và dải trạng thái vận hành.
- Analytics được xếp một cột toàn chiều rộng.
- Typography Dashboard, card ao, bộ lọc và heading được tăng kích thước.

## Xác thực và phân quyền
- Cookie authentication của ASP.NET Core.
- PBKDF2-HMAC-SHA256, khóa tạm thời sau nhiều lần sai.
- Role: Administrator, FarmManager, Technician, Viewer.
- Trang quản trị tài khoản `/admin/users`.
- Tài khoản bootstrap lấy từ biến môi trường khi dùng Docker.

## Quản lý vụ nuôi
- Collection `production_cycles`, `growth_samples`, `mortality_records`, `harvest_records`.
- Danh sách `/production-cycles` và trang riêng `/production-cycles/{id}`.
- Tính Biomass, tỷ lệ sống, tổng thức ăn, FCR và sản lượng thu hoạch.
- Worker mô phỏng đọc `Config/production-cycle-simulation.json`.

## Lưu ý kiểm thử
Môi trường tạo gói không có .NET SDK. Cần chạy `dotnet restore` và `dotnet build` trên máy phát triển trước khi đưa vào vận hành.

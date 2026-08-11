# MainLayout

## Menu điều hành

Menu được chia thành ba nhóm mở rộng/thu gọn:

1. `Phân tích tổng thể`: sáu miền phân tích toàn trại.
2. `Danh sách ao nuôi`: workspace riêng của từng ao.
3. `Quản lý ao đang chọn`: tổng quan, cảm biến, thiết bị, cho ăn và cảnh báo.

Dashboard tổng luôn nằm ở cấp đầu để người vận hành quay lại trung tâm điều hành nhanh nhất.

## Trạng thái dữ liệu

Khi `DashboardSimulation:Enabled=true`, topbar hiển thị nhãn `SIMULATION` nhằm tránh nhầm dữ liệu thử nghiệm với dữ liệu triển khai thực tế.

Topbar cũng có liên kết `Đổi mật khẩu` cho tài khoản đang đăng nhập. Liên kết mở trang `/account/change-password`, nơi người dùng phải xác nhận mật khẩu hiện tại trước khi đặt mật khẩu mới.

## Responsive

Sidebar chuyển thành drawer trên màn hình nhỏ. Các nhóm vẫn giữ trạng thái mở/đóng trong vòng đời circuit hiện tại.

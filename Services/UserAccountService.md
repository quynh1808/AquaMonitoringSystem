# UserAccountService

Cookie chứa SessionVersion. Khóa user, đổi quyền hoặc đổi/reset mật khẩu tăng version; middleware cookie đối chiếu MongoDB và thu hồi phiên cũ. Login input có giới hạn độ dài, password vẫn được hash PBKDF2 với salt riêng.

Quản lý tài khoản MongoDB, xác minh mật khẩu, khóa tạm thời, role, phạm vi ao và tạo `ClaimsPrincipal`. Tài khoản quản trị đầu tiên chỉ được tạo khi collection `users` rỗng.

Mật khẩu mới tối thiểu 12 ký tự. Service chặn khóa hoặc hạ quyền Administrator đang hoạt động cuối cùng để tránh tự khóa toàn bộ hệ thống.

- `ChangePasswordAsync` xác minh mật khẩu hiện tại và từ chối tái sử dụng chính mật khẩu đó.
- `ResetPasswordAsync` phục vụ Administrator reset user và xóa lockout.
- `ResetAdministratorPasswordAsync` là recovery cục bộ: chỉ nhận tài khoản có role Administrator, mở khóa/kích hoạt lại và không log secret.
- `PasswordChangedAtUtc` ghi thời điểm thay đổi gần nhất.

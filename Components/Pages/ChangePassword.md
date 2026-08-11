# ChangePassword

Trang `/account/change-password` cho người dùng đã đăng nhập tự đổi mật khẩu. Service xác minh mật khẩu hiện tại, từ chối mật khẩu mới dưới 12 ký tự hoặc trùng mật khẩu cũ, tạo salt/hash PBKDF2 mới và xóa trạng thái lockout.

Ba trường mật khẩu có nút `Hiện/Ẩn`; giá trị chỉ nằm trong state của Blazor circuit và không được ghi log.

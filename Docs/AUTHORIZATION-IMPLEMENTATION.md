# Đăng nhập và phân quyền

- Cookie HttpOnly, SameSite Strict và sliding expiration.
- PBKDF2-HMAC-SHA256; không lưu mật khẩu rõ.
- Vai trò: Administrator, FarmManager, Technician, Viewer.
- Policy: ManageUsers, ManageFarm, OperatePonds.
- `AllowedPondIds` được ánh xạ thành claim `pond_access`; `FarmAccessService` lọc dữ liệu đọc và chặn mọi mutation ngoài phạm vi ao.
- Thay mật khẩu bootstrap qua biến môi trường trước lần chạy đầu.
- Data Protection keys được persist qua Docker volume để cookie còn hợp lệ sau khi thay container.
- Viewer chỉ đọc; Technician vận hành ao được gán; FarmManager/Admin mới được cấu hình ao/thiết bị và mô phỏng thủ công.
- Mọi user đã đăng nhập tự đổi mật khẩu tại `/account/change-password`; chỉ Administrator được reset user khác tại `/admin/users`.
- Bootstrap password không đồng bộ lại user đã tồn tại. Trường hợp mất toàn bộ admin dùng local recovery command, không xóa collection `users`.

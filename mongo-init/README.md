# MongoDB init

`01-create-app-user.sh` chỉ chạy khi Docker khởi tạo volume MongoDB mới.

Nó tạo tài khoản ứng dụng có role `readWrite` riêng trên database `aquaculture_monitoring`. Web container dùng tài khoản này; tài khoản root chỉ dành cho quản trị, healthcheck, backup và restore.

Biến môi trường được khai báo trong `.env`:

- `MONGO_ROOT_USERNAME`
- `MONGO_ROOT_PASSWORD`
- `MONGO_APP_USERNAME`
- `MONGO_APP_PASSWORD`
- `MONGO_APP_DATABASE`

Nếu volume đã tồn tại trước khi thêm script, cần tạo user thủ công hoặc xóa volume trong môi trường thử nghiệm rồi khởi tạo lại.

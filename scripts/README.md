# Script vận hành

- `bootstrap-ubuntu.sh`: chạy một lần sau khi tạo `.env`; sinh file mật khẩu Mosquitto và kiểm tra Compose.
- `deploy-ubuntu.sh [image-tag]`: fast-forward source, kéo image GHCR, cập nhật stack và kiểm tra readiness. Nếu image mới không ready, script trả `.env` về tag trước và khởi động lại ứng dụng.
- `reset-admin-password.sh`: recovery admin trên Ubuntu bằng container chạy một lần; password nhập ẩn và không lưu vào `.env`.
- `reset-admin-password.ps1`: recovery trên Windows; mặc định dùng `dotnet run`, thêm `-Docker` nếu stack Compose đang chạy.
- `backup-mongodb.sh` / `restore-mongodb.sh`: backup và restore dữ liệu MongoDB; hãy thử restore định kỳ trên máy tách biệt.

Trên Ubuntu chạy `chmod +x scripts/*.sh` sau lần clone đầu tiên. Không chạy script deploy đồng thời từ nhiều phiên SSH.

## Recovery mật khẩu Administrator

Ubuntu/Compose, khi MongoDB đang chạy:

```bash
./scripts/reset-admin-password.sh
```

Windows với ứng dụng chạy trực tiếp và MongoDB theo `appsettings.json`:

```powershell
.\scripts\reset-admin-password.ps1
```

Windows với Compose:

```powershell
.\scripts\reset-admin-password.ps1 -Docker
```

Các script nhập password dạng ẩn, kiểm tra tối thiểu 12 ký tự và không ghi secret vào `.env`. Recovery chỉ áp dụng cho user đã có role `Administrator`; nó mở khóa/kích hoạt lại tài khoản và tạo PBKDF2 hash/salt mới.

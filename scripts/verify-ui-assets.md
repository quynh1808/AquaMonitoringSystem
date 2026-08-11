# verify-ui-assets.ps1

Kiểm tra nhanh trang chủ, trang đăng nhập và các tài nguyên UI sau khi chạy ứng dụng.

```powershell
./Scripts/verify-ui-assets.ps1
```

Hoặc chỉ định URL khác:

```powershell
./Scripts/verify-ui-assets.ps1 -BaseUrl "http://192.168.1.20:8080"
```

Script trả exit code `1` nếu asset bị redirect, không trả HTTP 200 hoặc trả sai content type.

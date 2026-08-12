# Kiểm thử tự động

Project `AquacultureMonitoring.Tests` kiểm tra các vùng có rủi ro cao nhưng không cần MongoDB thật:

- chuyển đổi Mitsubishi M/D sang địa chỉ Modbus TCP;
- PBKDF2 và so sánh mật khẩu constant-time;
- phân quyền theo role/phạm vi ao tại service;
- validation của safety switch điều khiển thiết bị.
- mặc định sensor chưa publish trước commissioning.

Chạy bằng `dotnet test Tests/AquacultureMonitoring.Tests.csproj`. Các integration test MongoDB/MQTT nên được bổ sung bằng container tạm trong CI trước khi bật phần cứng thật.

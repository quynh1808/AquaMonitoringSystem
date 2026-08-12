# Báo cáo kiểm tra source production

Ngày kiểm tra gần nhất: 12/08/2026

## Kết quả thực tế

- Clean restore theo `packages.lock.json`: đạt.
- Release build toàn solution: đạt, 0 warning, 0 error.
- Unit test: 6/6 đạt (Modbus CRC/decode, PBKDF2, pond authorization, device safety validation).
- Luồng tài khoản: có đổi mật khẩu cá nhân, quản trị viên reset mật khẩu và lệnh khôi phục admin không phụ thuộc phiên đăng nhập.
- Trường mật khẩu ở trang đăng nhập, tạo/reset tài khoản và đổi mật khẩu đều có nút hiện/ẩn; JavaScript không đọc hoặc lưu giá trị mật khẩu.
- `.NET SDK PublishContainer`: đạt; đã tạo OCI archive `aquaops:validation` trên base `mcr.microsoft.com/dotnet/aspnet:10.0` rồi xóa artifact kiểm tra.
- `docker compose config --quiet`: đạt với đầy đủ environment production mẫu.
- Locked restore `linux-x64` đúng như GitHub workflow: đạt.
- Secret scan: không phát hiện production credential hard-code; chỉ còn placeholder/default development đã ghi chú.
- Legacy `App_Data`, Dockerfile, Compose cũ và mọi `bin/obj`: đã xóa.

## Các thay đổi an toàn chính

- Mongo write queue và sensor bus có backpressure, không còn silent `TryWrite`.
- Projection và mutation kiểm role/phạm vi ao ở service.
- Login/logout có antiforgery; login có rate limit; Data Protection keys được persist.
- Init container chuẩn hóa quyền named volume Data Protection trước khi app non-root khởi động.
- Đổi/reset mật khẩu luôn tạo salt mới, xóa trạng thái khóa và không ghi mật khẩu vào file cấu hình hay tham số dòng lệnh.
- Device command ghi pending intent trước dispatch, chỉ cập nhật state sau ACK/read-back, có master hardware switch và không retry mù.
- MongoDB không publish host port; Kestrel chỉ publish HTTP trên địa chỉ/cổng LAN được cấu hình.
- Container app read-only, `no-new-privileges`; log có rotation.

## Chưa thể xác nhận khi chưa có hạ tầng thật

- Startup integration với MongoDB/Mosquitto trên Ubuntu và chính sách firewall LAN thật.
- MQTT ACL/TLS/mTLS, firewall LAN và độ ổn định gateway.
- PLC/relay adapter cụ thể, output feedback, interlock và E-stop.
- Load/soak test theo số Blazor circuit, sensor và message thực tế.
- Backup restore rehearsal trên dataset production gần đúng.

Không bật `HARDWARE_CONTROL_ENABLED` trước khi hoàn tất `Docs/HARDWARE-INTEGRATION.md`.

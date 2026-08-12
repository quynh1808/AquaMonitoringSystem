# AddDeviceModal

Form tạo hoặc sửa actuator dùng một Modbus TCP socket đã đăng ký. Administrator nhập riêng
START/STOP request, Auto request/feedback, Physical override, Remote allowed, Run feedback và
Fault M-variable. Server kiểm tra cú pháp, trùng địa chỉ và quan hệ socket/ao trước khi hiện hộp
xác nhận. Mapping chỉ có hiệu lực sau commissioning Apply/ACK; khi Physical override đang bật,
giao diện chỉ được giám sát và mọi lệnh từ xa đều bị chặn ở server.

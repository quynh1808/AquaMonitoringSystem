# Devices

Trang quản lý thiết bị toàn trại. Cho phép lọc theo ao, thêm thiết bị, đổi Manual/Auto và gửi lệnh bật/tắt qua `FarmStateService`. Bản hiện tại lưu `DeviceCommand` và ACK mô phỏng trong MongoDB; transport phần cứng sẽ được thay ở lớp command adapter sau này.

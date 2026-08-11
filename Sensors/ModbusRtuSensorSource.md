# ModbusRtuSensorSource

Adapter mở cổng serial RS-485/USB, gửi function 03 và kiểm tra slave ID, byte count và CRC16.

Trên Linux, Docker container cần được cấp thiết bị, ví dụ `/dev/ttyUSB0`. Quyền truy cập serial phải được cấu hình ở host; không chạy container privileged nếu không cần thiết.

Mỗi bus RTU nên có một polling task duy nhất để tránh hai master tranh chấp đường truyền.

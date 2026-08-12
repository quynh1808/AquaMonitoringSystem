# Tích hợp ESP32 và Mitsubishi FX5U

P03 dùng MQTT cho telemetry và Modbus TCP cho actuator. Hai đường độc lập nên có thể đưa probe thật vào trước trong khi `HARDWARE_CONTROL_ENABLED=false`.

## FX5U

Adapter hiện có là `fx5u-modbus-tcp`. Map mặc định:

- quạt: command/feedback/fault `M100/M101/M102`;
- máy cho ăn: `M200/M201/M202`;
- bơm: `M300/M301/M302`;
- heartbeat: `D100`.

Server ghi command, đọc fault rồi đọc feedback. Write ACK không đủ để hoàn tất lệnh. ESP32 simulator dùng port 1502; FX5U thật thường dùng port 502 theo cấu hình GX Works3.

## Safety bắt buộc tại tủ/PLC

- E-stop và manual override vật lý;
- bảo vệ overload/thermal, dry-run và chống đóng cắt nhanh;
- watchdog, power-on safe state và mất mạng về OFF;
- feedback từ tiếp điểm phụ nếu tải có rủi ro;
- interlock mức nước/DO cục bộ cho tải quan trọng.

Web app là supervisory control, không phải safety PLC. Quy trình đầy đủ và rollback: `Docs/OPERATING-MODES.md`.

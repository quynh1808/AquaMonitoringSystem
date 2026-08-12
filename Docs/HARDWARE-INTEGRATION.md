# Tích hợp ESP32 và Mitsubishi FX5U

P03 dùng MQTT cho telemetry và Modbus TCP cho actuator. Hai đường độc lập nên có thể đưa probe thật vào trước trong khi `HARDWARE_CONTROL_ENABLED=false`.

## FX5U

Adapter hiện có là `fx5u-modbus-tcp`. Server không chứa endpoint hoặc map M/D mặc định.
Administrator tạo một Modbus socket cho đúng PLC tại `Thiết lập hệ thống → Network & Connections`,
nhập các thanh ghi handshake, sau đó khai báo START/STOP, AUTO, local override, remote allowed,
run feedback và fault cho từng thiết bị. Tất cả được lưu trong MongoDB.

Server ghi request, đọc fault và feedback; write ACK của Modbus không đủ để hoàn tất lệnh.
ESP32 simulator dùng map minh họa trong `Docs/LEGACY-NODE-CONNECTION-REFERENCE.md`; FX5U thật dùng
đúng IP/port và M/D variables mà admin đã lập trình, phê duyệt trong GX Works3.

## Safety bắt buộc tại tủ/PLC

- E-stop và manual override vật lý;
- bảo vệ overload/thermal, dry-run và chống đóng cắt nhanh;
- watchdog, power-on safe state và mất mạng về OFF;
- feedback từ tiếp điểm phụ nếu tải có rủi ro;
- interlock mức nước/DO cục bộ cho tải quan trọng.

Web app là supervisory control, không phải safety PLC. Quy trình đầy đủ và rollback: `Docs/OPERATING-MODES.md`.

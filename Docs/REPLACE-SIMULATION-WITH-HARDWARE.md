# Thay mô phỏng bằng phần cứng

## Nguyên tắc

UI, MongoDB, engine cảnh báo và tự động hóa không phụ thuộc nguồn dữ liệu. Mọi adapter chuyển dữ liệu thành `SensorEnvelope` rồi đưa vào `SensorMessageBus`.

```text
Simulation / MQTT / Modbus TCP / Modbus RTU
                    ↓
              SensorEnvelope
                    ↓
             SensorMessageBus
                    ↓
          SensorIngestionWorker
                    ↓
 MongoDB + Runtime + Alert + Automation
```

## Chuyển sang MQTT

1. Mở `Config/sensor-connections.json`.
2. Đặt `ActiveSources` thành `["mqtt"]` hoặc giữ cả mô phỏng trong thời gian thử.
3. Đặt `Mqtt.Enabled` thành `true` và sửa host, port, tài khoản, TLS.
4. Nếu broker chạy bằng Docker Compose và thiết bị nằm trong LAN, đặt `MQTT_BIND_ADDRESS` trong `.env` thành IP LAN của server hoặc `0.0.0.0`; đồng thời cấu hình tài khoản, ACL và TLS cho Mosquitto.
5. Cho ESP32/Raspberry Pi publish JSON theo `Examples/mqtt-payload.json`.
6. Khởi động lại ứng dụng.

Không cần sửa component Razor hoặc service nghiệp vụ.

## Chuyển sang Modbus TCP

1. Bật một phần tử trong `ModbusTcp`.
2. Khai báo IP PLC/gateway, `UnitId`, địa chỉ thanh ghi, kiểu dữ liệu, byte order, scale và offset.
3. Đặt `modbus-tcp` trong `ActiveSources`.
4. Khởi động lại.

## Chuyển sang Modbus RTU

1. Gắn bộ USB–RS485 và xác định tên cổng.
2. Khai báo baud rate, parity, stop bits và mapping.
3. Với Docker Linux, ánh xạ `/dev/ttyUSB0` vào container.
4. Đặt `modbus-rtu` trong `ActiveSources` rồi khởi động lại.

## Chạy song song khi commissioning

Có thể giữ `simulation` và thêm một nguồn thật để lắp đặt từng phần. Khi bản tin MQTT/Modbus đầu tiên đến với cùng `SensorId`, ingestion cập nhật `SourceType`/`ExternalAddress`; bộ mô phỏng tự ngừng phát riêng sensor đó. Những sensor chưa có phần cứng vẫn tiếp tục mô phỏng.

Khi cần đối chứng đồng thời sensor thật và sensor giả, dùng `SensorId` khác nhau để cả hai luồng được lưu độc lập.

## Kiểm tra trước khi điều khiển

- Xem `Trạng thái adapter kết nối` tại trang cảm biến.
- Kiểm tra `LastSeen`, `Quality`, firmware, pin/RSSI.
- Đối chiếu số đo bằng dụng cụ chuẩn.
- Xác nhận byte order, scale và offset.
- Chỉ cho phép PLC thực hiện liên động an toàn; web không phải lớp bảo vệ duy nhất.

Telemetry ở tài liệu này không đồng nghĩa với quyền điều khiển. Quy trình command intent, ACK/read-back, master safety switch và commissioning từng thiết bị nằm trong `Docs/HARDWARE-INTEGRATION.md`.

# Commissioning động: socket, cảm biến và PLC

## 1. Nguyên tắc

- Container AquaOps là immutable; cấu hình node lưu trong MongoDB.
- Một socket logic ánh xạ đúng một node vật lý.
- MQTT broker profile dùng chung cho nhiều MQTT socket.
- Cảm biến chỉ nhận dữ liệu sau khi Administrator khai báo `SensorId + SocketId + NodeId + ChannelId`.
- Startup archive toàn bộ definition sensor/thiết bị demo cũ; readings, commands và audit lịch sử vẫn được giữ nhưng không tạo card vận hành.
- Chu kỳ `0` nghĩa là `NotConfigured`: firmware không đọc và không gửi channel.
- `Applying` chỉ thành `Applied` khi node trả đúng `CommandId` và version.
- PLC mapping M/D được nhập tay theo chương trình FX5U thực tế và được validate trước khi lưu.

## 2. Trình tự MQTT/ESP32

1. Vào **Thiết lập hệ thống → Network & Connections**, tạo MQTT Broker Profile.
2. Tạo MQTT socket, chọn ao, nhập NodeId và broker profile.
3. Provision ESP32 qua UART, restart và kiểm tra node publish `status`/`capabilities`.
4. Vào **Edge nodes** kiểm tra ChannelId và giới hạn vật lý firmware báo cáo.
5. Vào ao → **Cảm biến** → **Thêm cảm biến**, chọn socket/channel và đặt chu kỳ.
6. Quay lại Network & Connections, chọn **Apply** và nhập lý do.
7. Kiểm tra `Applying → Applied`; nếu node offline trạng thái là `PendingReconnect`.
8. ESP32 bắt đầu đọc đúng các channel có interval lớn hơn 0.

## 3. Trình tự PLC FX5U/ESP32 simulator

1. Tạo socket Modbus TCP với host, port, Unit ID, heartbeat và handshake M/D.
2. Tạo thiết bị, chọn socket và nhập START/STOP pulse, Local Override, Remote Allowed,
   Run Feedback và Fault.
3. Apply socket. Server ghi desired version, pulse Apply và chỉ nhận Applied khi ACK/version khớp.
4. Kiểm tra mode reported. Physical Override phải vô hiệu hóa mọi điều khiển web.
5. Chỉ bật `HARDWARE_CONTROL_ENABLED=true` sau khi interlock và emergency stop đã thử tại tủ.

## 4. MQTT contract

```text
aquaculture/{farm}/{pond}/nodes/{node}/status
aquaculture/{farm}/{pond}/nodes/{node}/capabilities
aquaculture/{farm}/{pond}/nodes/{node}/config/request
aquaculture/{farm}/{pond}/nodes/{node}/config/desired
aquaculture/{farm}/{pond}/nodes/{node}/config/reported
aquaculture/{farm}/{pond}/nodes/{node}/sensors/{sensorId}/telemetry
aquaculture/{farm}/{pond}/nodes/{node}/sensors/{sensorId}/health
aquaculture/{farm}/{pond}/nodes/{node}/sync/data
aquaculture/{farm}/{pond}/nodes/{node}/sync/ack
```

Node lạ, sai pond, sai topic hoặc sai mapping đi vào `unassigned_node_messages`, không tự tạo sensor.
MQTT subscriber không nhận được ClientId của publisher trong message callback. Vì vậy `Expected Client ID`
được dùng để provision/đối chiếu vận hành; danh tính node phải được broker cưỡng chế bằng credential riêng
và ACL chỉ cho phép publish đúng prefix mà AquaOps sinh từ PondId + NodeId.

## 5. Mất kết nối và dữ liệu tồn

- ESP32 lưu telemetry trong NVS ring buffer.
- MQTT QoS ACK không xóa dữ liệu.
- AquaOps chỉ ACK EventId sau khi ingestion xác nhận MongoDB đã ghi bền vững/idempotent.
- Node gửi lại theo batch có throttle; dữ liệu backfill không rewind dashboard và không chạy automation cũ.
- Command actuator cũ không được tự động replay.
- PLC reconnect chỉ đọc lại trạng thái hiện tại nếu PLC không tự lưu history.
- Sensor firmware dùng partition NVS 1 MiB và hàng đợi bounded để kiểm soát dung lượng/wear. Với mất
  mạng dài ngày hoặc tần suất cao, thay NVS queue bằng LittleFS/FRAM/SD công nghiệp và kiểm thử endurance.
- Nếu node cold-boot khi hoàn toàn offline, không tự tạo UTC; dùng RTC DS3231 khi cần timestamp chính xác dài ngày.

## 6. Thay cảm biến

1. Chọn **Thay cảm biến**, nhập lý do và xác định có tắt toàn node không.
2. Nếu tắt node, socket là `MaintenanceOffline`; sensor phụ thuộc là `UnavailableViaNode`.
3. Lắp đầu dò mới, nhập serial/model/manufacturer.
4. AquaOps giữ `SensorId`, đóng installation cũ và tạo `SensorInstallationId` mới.
5. Apply config và chờ self-test/health trước khi dùng dữ liệu cho automation.

## 7. Thiết bị và AUTO

Ưu tiên: `Safety > Physical Local > Remote Manual > Auto`.

- Remote Manual dùng hai pulse M riêng cho START và STOP.
- AUTO dùng policy MongoDB với hysteresis, persistence, minimum ON/OFF và maximum starts/hour.
- PLC vẫn phải có interlock/anti-short-cycle độc lập; web không thay thế bảo vệ điện và cơ khí.

## 8. Xóa cảm biến và thiết bị

Đây là soft-delete có ACK, không phải xóa document MongoDB:

1. Administrator bấm **Xóa**, đọc cảnh báo và nhập lý do.
2. Asset bị disable, ẩn khỏi Tổng quan/Dashboard/Analytics và không còn được automation sử dụng.
3. Nếu asset vẫn đang nằm trong reported configuration, AquaOps tạo desired version mới không chứa asset.
4. Node offline giữ trạng thái `PendingReconnect`; timeout/rejected phải được xử lý và Apply lại.
5. Chỉ khi node/PLC ACK đúng version, asset mới chuyển `Archived`.
6. Asset Draft chưa từng Applied được archive ngay vì node chưa từng nhận mapping đó.

Sensor không thể xóa khi còn AUTO policy Enable. Thiết bị không thể xóa nếu còn AUTO policy, đang
chạy, fault, PLC/feedback offline hoặc Physical/Local Override. Operator phải STOP và xác nhận Run
Feedback OFF trước. Rollback cấu hình luôn lọc asset pending/archived nên không thể làm sống lại mapping
đã ngừng sử dụng. Reading, sensor installation, command history và audit được giữ để truy vết.

# Tích hợp relay/PLC và loại bỏ simulation theo từng bước

## Ranh giới thiết kế

Telemetry dùng `ISensorDataSource -> SensorMessageBus`. Điều khiển actuator dùng `DeviceCommandService -> IDeviceCommandTransport`. Hai đường không dùng chung safety switch, vì có thể đọc sensor thật nhiều tuần trước khi cho phép web gửi lệnh thật.

Mỗi `Device` có:

- `ControlTransport`: tên adapter, ví dụ `simulation`, `mqtt-relay`, `modbus-tcp-coil`;
- `ExternalAddress`: mapping ổn định do adapter hiểu, không hard-code địa chỉ PLC trong UI;
- `FailSafeState`: trạng thái PLC phải áp dụng khi mất liên lạc/nguồn;
- `Status`: chỉ cập nhật sau ACK/read-back thành công.

`DeviceCommandService` ghi intent `pending` trước dispatch, chặn toàn bộ hardware khi `HardwareControlEnabled=false`, áp timeout, lưu audit và không tự retry một lệnh có thể đã tác động vật lý.

## Cách thêm adapter thật

1. Tạo class trong `Devices/` triển khai `IDeviceCommandTransport`.
2. Đặt `Name` duy nhất và đăng ký singleton trong `Program.cs`.
3. Parse/validate `ExternalAddress` bằng schema riêng của adapter.
4. Tạo correlation id từ command/device id cho giao thức có ACK.
5. Chỉ trả `Success=true` sau ACK đúng correlation hoặc đọc lại output/state PLC.
6. Timeout/connection loss trả trạng thái không thành công; không đoán rằng output đã đổi.
7. Viết unit test codec/mapping và integration test với PLC simulator/test bench.

Adapter không chứa authorization, UI hoặc MongoDB. Điều đó cho phép thay MQTT relay bằng Modbus/OPC UA mà không đổi business service.

Nếu Modbus RTU adapter gắn trực tiếp vào Ubuntu, bật `compose.hardware.yaml` qua `COMPOSE_FILE` trong `.env`, dùng đường dẫn ổn định `/dev/serial/by-id/...` và xác nhận `DIALOUT_GID` bằng `getent group dialout`. Không dùng `/dev/ttyUSB0` làm định danh host lâu dài vì số thứ tự có thể đổi sau reboot/replug.

## Commissioning bảy giai đoạn

1. **Simulation toàn phần:** `ActiveSources=[simulation]`, tất cả device dùng transport `simulation`, hardware switch false.
2. **Shadow telemetry:** thêm MQTT/Modbus sensor thật nhưng giữ simulation cho sensor chưa lắp. Đối chiếu thời gian, unit, scale, byte order, chất lượng.
3. **Telemetry thật:** bỏ simulation theo từng `SensorId`; theo dõi offline, reconnect và retention ít nhất vài chu kỳ vận hành.
4. **PLC dry-run:** adapter thật gửi vào PLC test/bit shadow không đấu tải. Xác minh command id, ACK, timeout, duplicate và mất mạng.
5. **Một thiết bị pilot:** đặt `ControlTransport`/`ExternalAddress` cho đúng một thiết bị, giữ master switch false; kiểm tra interlock/E-stop tại tủ.
6. **Cửa sổ bảo trì có giám sát:** bật `HARDWARE_CONTROL_ENABLED=true`, chạy manual on/off pilot, đối chiếu output feedback và audit; tắt switch ngay sau test nếu chưa nghiệm thu.
7. **Mở rộng từng thiết bị:** chuyển mapping theo nhóm nhỏ, theo dõi production; chỉ khi ổn định mới `DEVICE_SIMULATION_ENABLED=false` và bỏ simulation còn lại.

## Safety bắt buộc tại PLC/relay

- emergency stop vật lý và manual override;
- overload/thermal protection, dry-run, minimum on/off time và chống đóng cắt nhanh;
- watchdog, safe state khi mất mạng và power-on default;
- giới hạn mức nước/DO cục bộ cho tải quan trọng;
- output feedback/contactor auxiliary contact nếu thiết bị có rủi ro cao.

Web app là supervisory control, không phải safety PLC. `FailSafeState` trong database là metadata/audit; PLC phải thực thi fail-safe thật.

## Rollback phần cứng

Để lùi một thiết bị, đặt lại `ControlTransport=simulation` hoặc giữ adapter thật nhưng `HARDWARE_CONTROL_ENABLED=false`. Sau đó dùng local/manual control tại tủ. Không rollback database `Status` để giả vờ thiết bị đã đổi; luôn read-back/kiểm tra vật lý.

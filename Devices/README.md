# Device command transport

`Fx5uModbusTcpDeviceTransport` là boundary giữa nghiệp vụ AquaOps và PLC. Adapter chuyển tên M-device sang coil FX5, ghi command bằng FC05, đọc fault/feedback bằng FC01 và chỉ báo thành công khi read-back đúng.

`Fx5uPlcMonitor` đọc `D100` bằng FC03 và trạng thái thiết bị độc lập với thao tác UI. Mất heartbeat/kết nối không được giữ một trạng thái `on` cũ trên dashboard.

Map mặc định nằm trong `Config/device-control.json`; endpoint được ghi đè bằng `FX5U_HOST/PORT/UNIT_ID`. Emergency stop, overload, dry-run, minimum on/off time và watchdog tải phải nằm trong PLC/tủ điện.

# Device command transport

`Fx5uModbusTcpDeviceTransport` là boundary giữa nghiệp vụ AquaOps và PLC. Adapter chuyển tên M-device sang coil FX5, ghi command bằng FC05, đọc fault/feedback bằng FC01 và chỉ báo thành công khi read-back đúng.

`DynamicModbusConnectionSupervisor` đọc heartbeat, mode, local override, remote-allowed,
run feedback và fault độc lập với thao tác UI. Mất heartbeat/kết nối không được giữ một
trạng thái `on` cũ trên dashboard.

Endpoint và toàn bộ mapping M/D được Administrator khai báo tại `Thiết lập hệ thống →
Network & Connections`, lưu trong MongoDB và chỉ có hiệu lực sau chu trình Apply/ACK.
Emergency stop, overload, dry-run, minimum on/off time và watchdog tải vẫn phải nằm trong
PLC/tủ điện; web server không thay thế lớp an toàn vật lý này.

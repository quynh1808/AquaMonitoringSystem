# Device command transport

`Fx5uModbusTcpDeviceTransport` là boundary giữa nghiệp vụ AquaOps và PLC. Adapter chuyển tên M-device sang coil FX5, ghi command bằng FC05, đọc fault/feedback bằng FC01 và chỉ báo thành công khi read-back đúng.

`DynamicModbusConnectionSupervisor` đọc heartbeat, mode, local override, remote-allowed,
run feedback và fault độc lập với thao tác UI. Mất heartbeat/kết nối không được giữ một
trạng thái `on` cũ trên dashboard.

Endpoint và toàn bộ mapping M/D được Administrator khai báo tại `Thiết lập hệ thống →
Network & Connections`, lưu trong MongoDB và chỉ có hiệu lực sau chu trình Apply/ACK.
Emergency stop, overload, dry-run, minimum on/off time và watchdog tải vẫn phải nằm trong
PLC/tủ điện; web server không thay thế lớp an toàn vật lý này.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

`Fx5uModbusTcpDeviceTransport` là boundary giữa nghiệp vụ AquaOps và PLC. Adapter chuyển tên M-device sang coil FX5, ghi command bằng FC05, đọc fault/feedback bằng FC01 và chỉ báo thành công khi read-back đúng.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Device command transport**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Use case điều khiển phải tách khỏi frame Modbus để có thể thay transport/PLC và kiểm tra safety ở một cổng thống nhất.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Chuyển DeviceCommand thành thao tác PLC, polling feedback và trạng thái kết nối.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Service gọi interface transport; adapter FX5U dùng ModbusTcpProtocol; supervisor quản lý socket động và reconcile desired/reported.

- **Devices/DynamicModbusConnectionSupervisor.cs** (203 dòng): các điểm vào nhận diện được gồm DynamicModbusConnectionSupervisor.
- **Devices/Fx5uModbusTcpDeviceTransport.cs** (130 dòng): các điểm vào nhận diện được gồm Fx5uModbusTcpDeviceTransport.
- **Devices/IDeviceCommandTransport.cs** (35 dòng): các điểm vào nhận diện được gồm DeviceTransportRequest, DeviceTransportResult, IDeviceCommandTransport, IDeviceModeTransport.
- **Devices/ModbusTcpProtocol.cs** (107 dòng): các điểm vào nhận diện được gồm ModbusTcpProtocol.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Services/DeviceCommandService.cs, Services/AutomationEngineService.cs, Models/SystemConfigurationModels.cs, firmware PLC simulator và PLC thật.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Modbus TCP**: Giao thức request-response PLC qua TCP. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Coil M**: Biến bit trong memory map FX5U. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Register D**: Thanh ghi 16 bit. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Feedback**: Trạng thái thực PLC xác nhận. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Interlock**: Điều kiện cưỡng chế ngăn thao tác nguy hiểm. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra transaction ID, timeout, Modbus exception, reconnect, feedback OFF/ON, local override và fail-safe.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

# ESP32 sensor node

Firmware ESP-IDF cho WROOM-32E có sáu **capability channel**, nhưng tất cả mặc định interval `0`.
Chỉ channel được admin ánh xạ thành sensor và Apply/ACK thành công mới được đọc/gửi. Code hiện mô phỏng
giá trị tại edge; khi lắp probe thật, thay phần driver đọc channel và giữ nguyên protocol.

Tính năng: MQTT LWT/status, capabilities, desired/reported config, sensor health, NVS configuration,
EventId/sequence, NVS ring buffer và application-level sync ACK.

## Build bằng PlatformIO

Từ thư mục này:

~~~powershell
pio run -e aquaops_sensor_node
pio run -e aquaops_sensor_node -t upload --upload-port COM5
pio device monitor -p COM5 -b 115200
~~~

Thay `COM5` bằng port từ `pio device list`. Đọc theo thứ tự:

1. [Kiến trúc PlatformIO](../PLATFORMIO.md).
2. [Runbook chạy sensor node](PLATFORMIO-RUNBOOK.md).
3. [Xử lý lỗi](PLATFORMIO-TROUBLESHOOTING.md).
4. [Provisioning và khóa NVS](../../Docs/13.ESP32-PROVISIONING.md).

Không commit `.pio`, `managed_components`, `sdkconfig.aquaops_sensor_node` hoặc secret. Commit
`dependencies.lock` vì file này khóa version/hash MQTT và cJSON managed components. File
`platformio.ini` pin platform, chọn board 4 MB, ESP-IDF, source `main` và custom partition table.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Firmware ESP-IDF cho WROOM-32E có sáu **capability channel**, nhưng tất cả mặc định interval `0`. Chỉ channel được admin ánh xạ thành sensor và Apply/ACK thành công mới được đọc/gửi. Code hiện mô phỏng giá trị tại edge; khi lắp probe thật, thay phần driver đọc channel và giữ nguyên protocol.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **ESP32 sensor node**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Logic edge phải tiếp tục lấy mẫu, báo health và lưu dữ liệu qua mất mạng, đồng thời tách khỏi web server.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Chạy trên ESP32 sensor node hoặc simulator PLC và thực hiện contract commissioning với server.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

ESP-IDF build các FreeRTOS task, Wi-Fi/MQTT/Modbus, NVS và provisioning component; cấu hình nghiệp vụ nhận động sau bootstrap.

- Tài liệu này là runbook hoặc hồ sơ quyết định; source có thẩm quyền được liên kết trong nội dung và trong bản đồ toàn repository.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Sensors, Devices, Docs/13.ESP32-PROVISIONING.md, 09.DYNAMIC-COMMISSIONING.md và 12.HARDWARE-INTEGRATION.md.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **ESP-IDF**: SDK chính thức cho ESP32. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **FreeRTOS task**: Tác vụ lập lịch trên firmware. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **NVS**: Kho key-value bền vững trên flash. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Provisioning**: Nạp cấu hình bootstrap. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Store-and-forward**: Lưu khi offline và gửi bù. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build bằng ESP-IDF, flash thiết bị thử, xem serial log và kiểm tra reboot, reconnect, queue, ACK và recovery.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../../Docs/02.TECHNICAL-GLOSSARY.md)

# Firmware edge AquaOps

- `esp32_sensor_node`: Edge Simulation/production sensor node, nhận desired config theo MQTT,
  mỗi channel mặc định interval 0, gửi telemetry + sensor health và giữ offline data trong NVS.
- `esp32_fx5u_plc_simulator`: WROOM-32E mô phỏng phần Modbus TCP cần cho FX5U-32MT/D,
  gồm heartbeat, config ACK/version, ba mode, START/STOP pulse, feedback và fault.
- `components/aqua_provisioning`: bootstrap Wi-Fi/broker/node identity qua UART và NVS dùng chung.

Không commit `sdkconfig` có secret. Quy trình: `Docs/13.ESP32-PROVISIONING.md`; chuyển Edge Simulation
sang phần cứng thật: `Docs/11.OPERATING-MODES.md`.

## Tài liệu PlatformIO

- [Kiến trúc build PlatformIO](PLATFORMIO.md): giải thích PlatformIO, ESP-IDF, CMake, Kconfig,
  partition, artifact và ranh giới cấu hình runtime.
- [Runbook ESP32 sensor node](esp32_sensor_node/PLATFORMIO-RUNBOOK.md): cài đặt trên Windows,
  build, flash, UART provisioning, MQTT/AquaOps commissioning và acceptance test.
- [Xử lý lỗi PlatformIO/ESP32](esp32_sensor_node/PLATFORMIO-TROUBLESHOOTING.md): khoanh vùng lỗi từ
  toolchain, USB, Wi-Fi, SNTP, MQTT đến Apply/ACK và offline replay.

Cấu hình `platformio.ini` hiện chỉ áp dụng cho `esp32_sensor_node`. PLC simulator vẫn dùng ESP-IDF
CLI cho đến khi có environment PlatformIO riêng đã được kiểm chứng.

Sensor node dùng ESP-IDF 6.0.1; MQTT/cJSON được pin trong `main/idf_component.yml` và
`dependencies.lock`. Không commit thư mục tải về `managed_components`.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

- `esp32_sensor_node`: Edge Simulation/production sensor node, nhận desired config theo MQTT,   mỗi channel mặc định interval 0, gửi telemetry + sensor health và giữ offline data trong NVS. - `esp32_fx5u_plc_simulator`: WROOM-32E mô phỏng phần Modbus TCP cần cho FX5U-32MT/D,   gồm heartbeat, config ACK/version, ba mode, START/STOP pulse, feedback và fault. - `components/aqua_provisioning`: bootstrap Wi-Fi/broker/node identity qua UART và NVS dùng chung.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Firmware edge AquaOps**, thay vì chỉ biết tên class, page hoặc quy trình.

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

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

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

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

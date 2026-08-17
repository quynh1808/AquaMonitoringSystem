# Ví dụ đưa dữ liệu thật vào pipeline

## MQTT payload

`mqtt-payload.json` dùng đúng cấu trúc `SensorEnvelope` mà server nhận. Trước khi thử,
Administrator phải tạo broker, socket `sensor-node-01`, capability và sensor tương ứng trên web,
sau đó Apply thành công. Payload từ node chưa đăng ký hoặc sai socket/pond sẽ vào quarantine.

## Thử bằng Mosquitto CLI

PowerShell:

```powershell
./Examples/publish-mqtt.ps1
```

Linux:

```bash
./Examples/publish-mqtt.sh
```

Đặt đúng host, username/password rồi publish vào topic
`aquaculture/farm_main/pond_01/nodes/sensor-node-01/sensors/{sensorId}/telemetry`.

## Lưu ý timestamp

Gateway phải ưu tiên UTC ISO-8601 và gửi `eventId` cùng `sequenceNumber`. Server kiểm tra chéo pond/metric giữa payload và topic.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

`mqtt-payload.json` dùng đúng cấu trúc `SensorEnvelope` mà server nhận. Trước khi thử, Administrator phải tạo broker, socket `sensor-node-01`, capability và sensor tương ứng trên web, sau đó Apply thành công. Payload từ node chưa đăng ký hoặc sai socket/pond sẽ vào quarantine.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Ví dụ đưa dữ liệu thật vào pipeline**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Developer cần thử contract MQTT mà không phụ thuộc firmware/hardware.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Cung cấp payload và lệnh smoke test thủ công, không phải nguồn dữ liệu production.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Thay NodeId, SensorId, topic, timestamp và credential rồi publish bằng Mosquitto CLI; quan sát log, quarantine và UI.

- **Examples/mqtt-payload.json** (18 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.
- **Examples/publish-mqtt.ps1** (8 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.
- **Examples/publish-mqtt.sh** (5 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Sensors/SensorEnvelope.cs, DynamicMqttConnectionSupervisor.cs, firmware sensor node và Docs/09.DYNAMIC-COMMISSIONING.md.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Payload**: Nội dung message. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Topic**: Kênh phân loại MQTT. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **QoS**: Mức bảo đảm giao message. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Contract**: Cấu trúc và hành vi hai phía thống nhất. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Thử payload hợp lệ, ID không khai báo, EventId lặp, timestamp cũ và broker mất kết nối.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

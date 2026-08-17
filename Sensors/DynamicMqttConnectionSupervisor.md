# DynamicMqttConnectionSupervisor

## Mục đích

Background service này biến các NodeSocket MQTT trong MongoDB thành kết nối broker đang hoạt động. Nó thay thế cấu hình MQTT hard-code trong source.

## Vòng đời

Supervisor định kỳ reconcile danh sách socket enabled, tạo hoặc dừng session, retry có backoff và ghi connection log. Mỗi session subscribe namespace của NodeId và xử lý capability, telemetry, health, reported configuration và sync ACK.

## Quan hệ với ingestion

Supervisor chỉ xác thực protocol/topic ở lớp kết nối và chuyển telemetry thành SensorEnvelope lên SensorMessageBus. SensorIngestionWorker mới kiểm tra mapping SensorId/ChannelId, quality, EventId và persistence.

## Quan hệ với commissioning

Desired configuration được publish retained với version. Khi node báo đúng reported version, supervisor cập nhật deployment/asset thành Applied và finalize asset đang pending removal. ACK cũ hoặc sai version không được phép hoàn tất deployment mới.

## Khả năng phục hồi

ESP32 giữ EventId và dữ liệu offline; sau reconnect supervisor nhận backfill, server deduplicate và gửi sync ACK. Lỗi một socket không được làm dừng các socket khác.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Background service này biến các NodeSocket MQTT trong MongoDB thành kết nối broker đang hoạt động. Nó thay thế cấu hình MQTT hard-code trong source.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **DynamicMqttConnectionSupervisor**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Dữ liệu edge không đáng tin tuyệt đối và không được đi thẳng vào dashboard/database mà thiếu mapping, validation và chống trùng.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Quản lý kết nối MQTT, hàng đợi ingestion, quality, health, offline và maintenance.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Supervisor tạo envelope; bounded bus chuyển message; worker validate/persist/update runtime; monitor đánh giá thời gian và health.

- **Sensors/DynamicMqttConnectionSupervisor.cs** (378 dòng): các điểm vào nhận diện được gồm DynamicMqttConnectionSupervisor.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Firmware sensor node, Models, Data, Runtime, AlertEngineService, AutomationEngineService và UI telemetry.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Telemetry**: Dữ liệu đo từ edge. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Ingestion**: Nhận, chuẩn hóa, kiểm tra và lưu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **EventId**: Định danh dùng chống bản lặp. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Quality**: Mức tin cậy của reading. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Quarantine**: Nơi giữ message không được nhập dữ liệu chính. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra message đúng/sai/trùng, mapping thiếu, queue đầy, mất mạng, reconnect/backfill, stale/offline và shutdown.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

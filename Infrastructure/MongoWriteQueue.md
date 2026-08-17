# MongoWriteQueue

The queue serializes non-critical UI persistence work through a bounded channel. Producers use
`EnqueueAsync`, which waits when capacity is exhausted. This backpressure is intentional: the
previous `TryWrite` API could return `false` and silently lose a farm mutation when the queue was
full.

Các event UI hiện tại là đồng bộ. Để không khóa Blazor circuit khi ghi MongoDB, thao tác UI cập nhật runtime trước rồi đưa lệnh ghi vào channel nền.

Pipeline cảm biến vẫn ghi `sensor_readings` trực tiếp và `await` để đảm bảo mẫu đã được lưu trước khi xử lý tiếp. Queue chủ yếu dùng cho thao tác người dùng như thêm ao, bật thiết bị, ghi thức ăn và đóng cảnh báo.

Channel có giới hạn để tránh tăng RAM vô hạn khi MongoDB mất kết nối. Lỗi ghi được log và không làm sập circuit UI.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

The queue serializes non-critical UI persistence work through a bounded channel. Producers use `EnqueueAsync`, which waits when capacity is exhausted. This backpressure is intentional: the previous `TryWrite` API could return `false` and silently lose a farm mutation when the queue was full.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **MongoWriteQueue**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Các quan tâm kỹ thuật như health và backpressure dùng xuyên module nhưng không thuộc nghiệp vụ ao.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Cung cấp primitive độ tin cậy cho host và pipeline dữ liệu.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Được đăng ký qua DI/hosted service và có cancellation, capacity, timeout, structured logging.

- **Infrastructure/MongoWriteQueue.cs** (47 dòng): các điểm vào nhận diện được gồm IMongoWriteQueue, MongoWriteQueue.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Program.cs, MongoDB, Sensors, Runtime và Compose healthcheck.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Backpressure**: Làm chậm producer khi consumer quá tải. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Bounded queue**: Hàng đợi giới hạn dung lượng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Health check**: Kiểm tra máy-đọc về sức khỏe. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Cancellation**: Tín hiệu dừng có kiểm soát. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra quá tải, dependency chậm/offline, shutdown và endpoint health.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

# AquacultureModels

Chứa các document MongoDB và DTO dùng bởi UI.

## Document chính

- `Pond`: thông tin ao.
- `TelemetryData`: snapshot mới nhất theo ao.
- `SensorDefinition`: khai báo và sức khỏe sensor.
- `SensorReading`: bản ghi lịch sử append-only cùng metadata.
- `Device`, `DeviceCommand`: trạng thái và vòng đời lệnh.
- `AlertItem`, `AlarmRule`: sự kiện và rule cảnh báo.
- `CalibrationRecord`, `MaintenanceTask`: hiệu chuẩn và bảo trì.
- `AuditLog`: nhật ký thao tác.
- `DataSourceHealth`: sức khỏe adapter.
- `AppUser`: identity, PBKDF2 hash/salt, role, phạm vi ao, lockout và thời điểm đổi mật khẩu gần nhất.

## DTO UI

Các request thêm ao, thiết bị, thức ăn và mô phỏng được giữ tách khỏi document MongoDB để validation form không làm thay đổi schema lưu trữ.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Chứa các document MongoDB và DTO dùng bởi UI.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **AquacultureModels**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Mọi layer phải dùng cùng vocabulary và phân biệt document bền vững, DTO request và view model.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Định nghĩa schema logic/contract dữ liệu của toàn hệ thống.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Class/record được Mongo serializer, repository, service, worker và component dùng; model không tự thực hiện I/O.

- **Models/AquacultureModels.cs** (560 dòng): các điểm vào nhận diện được gồm Pond, TelemetryData, SensorDefinition, SensorReadingMetadata, SensorReading, Device, DeviceCommand, AlertItem, AlertNotification, AlarmRule, FeedLog, AutomationLog.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Data, Services, Sensors, Devices, Runtime và Components.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Model**: Kiểu biểu diễn dữ liệu/domain. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Document**: Model được lưu trong MongoDB. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **DTO**: Object mang dữ liệu giữa ranh giới. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **View model**: Dữ liệu đã định hình cho UI. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Backward compatibility**: Khả năng đọc dữ liệu/contract phiên bản cũ. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build toàn solution, kiểm tra serialization/default/migration và test mọi invariant bị ảnh hưởng.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

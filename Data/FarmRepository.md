# FarmRepository

Repository gom các thao tác PostgreSQL mà UI và pipeline cảm biến cần dùng.

Nguyên tắc:

1. `sensor_readings` chỉ thêm mới; reading và `latest_sensor_states` commit trong cùng transaction.
2. Backfill không cập nhật latest state nên không làm lùi Dashboard sau reconnect.
3. Snapshot dựng `TelemetryData` runtime từ các latest row động; không còn bảng sáu metric cố định.
4. Thông tin sensor/device/alert được upsert theo `Id` bằng DbContext ngắn hạn.
5. Xóa ao là archive transaction: giữ reading, command, audit và hồ sơ sản xuất.
6. Device command được upsert theo `Id`: cùng một row đi từ `pending` sang ACK cuối.
7. Blazor component không gọi EF Core/Npgsql trực tiếp.

Khi cần tách backend thành Web API, repository này có thể được chuyển nguyên sang project Infrastructure.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Repository gom các thao tác PostgreSQL mà UI và pipeline cảm biến cần dùng.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **FarmRepository**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

PostgreSQL access, index và migration cần tập trung để quy tắc persistence không bị trộn vào UI hoặc use case.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Là ranh giới query/mutation cho snapshot ao, telemetry, alert, command và lịch sử vận hành.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

AquaDbContext định nghĩa table/constraint; repository tạo DbContext ngắn hạn cho query/transaction; initializer áp dụng migration và baseline idempotent.

- **Data/FarmRepository.cs** (131 dòng): các điểm vào nhận diện được gồm FarmRepository.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Models, Services, Runtime, Configuration/AquacultureOptions.cs và PostgreSQL container.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Row/entity**: Bản ghi quan hệ và object EF Core tương ứng. Repository chịu trách nhiệm chuyển giữa entity và runtime projection.
- **Repository**: Lớp bao bọc query và ghi dữ liệu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Index**: Cấu trúc tăng tốc và có thể ép uniqueness. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Migration**: Chuyển dữ liệu cũ sang mô hình mới. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Idempotent**: Chạy lặp vẫn cho cùng trạng thái cuối. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra database rỗng, database đã có dữ liệu, initializer chạy lặp, index và việc giữ lịch sử.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

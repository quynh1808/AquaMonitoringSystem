# FarmOperationsService

Ranh giới nghiệp vụ và authorization của trang vận hành P2. Service kiểm tra quyền theo role/ao, xác thực cycle thuộc pond, cập nhật tồn kho nguyên tử và ghi audit cho mọi mutation. Trang Razor chỉ gọi service này, không truy cập MongoDB trực tiếp.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Ranh giới nghiệp vụ và authorization của trang vận hành P2. Service kiểm tra quyền theo role/ao, xác thực cycle thuộc pond, cập nhật tồn kho nguyên tử và ghi audit cho mọi mutation. Trang Razor chỉ gọi service này, không truy cập MongoDB trực tiếp.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **FarmOperationsService**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Quy tắc nghiệp vụ, authorization và điều phối side effect cần nằm server-side, độc lập khỏi markup và chi tiết database/protocol.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Hiện thực use case giữa Components, Data, Runtime, Sensors/Devices và hệ thống ngoài.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Service validate input/quyền/invariant, gọi repository hoặc transport, cập nhật runtime/audit và trả kết quả có nghĩa cho UI.

- **Services/FarmOperationsService.cs** (269 dòng): các điểm vào nhận diện được gồm FarmOperationsService.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Models, Data repositories, Runtime, Components, Sensors, Devices và Configuration.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Use case**: Một mục tiêu người dùng/hệ thống hoàn chỉnh. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Business rule**: Quy tắc nghiệp vụ phải được bảo vệ. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Authorization**: Kiểm tra quyền thực hiện. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Audit**: Ghi ai làm gì và khi nào. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Idempotency**: Retry không tạo tác dụng lặp. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build và test success path, dữ liệu sai, thiếu quyền, retry, concurrency và failure của phụ thuộc.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

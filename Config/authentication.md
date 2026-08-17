# authentication.json

Cấu hình tài khoản quản trị khởi tạo, số vòng PBKDF2, giới hạn đăng nhập sai, thời gian khóa và thời lượng cookie. Trong production phải ghi đè mật khẩu bằng biến môi trường `Authentication__BootstrapAdminPassword` trước lần chạy đầu tiên.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Cấu hình tài khoản quản trị khởi tạo, số vòng PBKDF2, giới hạn đăng nhập sai, thời gian khóa và thời lượng cookie. Trong production phải ghi đè mật khẩu bằng biến môi trường `Authentication__BootstrapAdminPassword` trước lần chạy đầu tiên.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **authentication.json**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Cấu hình cần thay đổi theo môi trường mà không biên dịch lại, nhưng vẫn phải có schema, nguồn ưu tiên và quy tắc không lưu secret production.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Cung cấp catalog/default/seed cho Options, initializer và service.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

JSON được nạp bởi configuration pipeline hoặc initializer. Environment có thể override; cấu hình commissioning động trong MongoDB là nguồn có thẩm quyền cho node.

- Tài liệu này là runbook hoặc hồ sơ quyết định; source có thẩm quyền được liên kết trong nội dung và trong bản đồ toàn repository.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Configuration/*.cs, appsettings.json, Program.cs, Data/MongoDatabaseInitializer.cs và .env.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Configuration precedence**: Nguồn ưu tiên cao ghi đè nguồn thấp. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Options binding**: Ánh xạ cấu hình sang class C#. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Seed data**: Dữ liệu chỉ tạo khi database ban đầu rỗng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Secret**: Credential hoặc token không được commit. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra JSON hợp lệ, validator chấp nhận trường hợp tốt và từ chối giá trị nguy hiểm; không commit credential thật.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

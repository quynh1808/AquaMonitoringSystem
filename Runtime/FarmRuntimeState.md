# FarmRuntimeState

Đây là cache runtime dùng chung giữa Dashboard và pipeline cảm biến.

- PostgreSQL vẫn là nguồn dữ liệu bền vững.
- Runtime state giữ snapshot mới nhất để Blazor render nhanh và phát sự kiện `Changed`.
- Lịch sử gần nhất được giữ trong RAM theo `RuntimeReadingLimit`; lịch sử đầy đủ nằm trong PostgreSQL.
- Mọi cập nhật đều đi qua method có khóa đồng bộ, tránh component đọc collection trong lúc worker đang sửa.

Khi triển khai nhiều instance web server, runtime state nên được thay bằng PostgreSQL change stream/Redis pub-sub để đồng bộ giữa các instance.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Đây là cache runtime dùng chung giữa Dashboard và pipeline cảm biến.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **FarmRuntimeState**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

UI và worker cần snapshot nhanh nhưng PostgreSQL vẫn phải là nguồn bền vững.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Giữ cache thread-safe và bảo đảm database được bootstrap trước tác vụ phụ thuộc.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Hosted service chạy initializer/load; FarmRuntimeState upsert snapshot và phát Changed cho component.

- **Runtime/FarmRuntimeState.cs** (404 dòng): các điểm vào nhận diện được gồm FarmRuntimeState.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Data, Services/FarmStateService.cs, Sensors workers và Components.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Runtime state**: Trạng thái sống trong RAM. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Snapshot**: Ảnh chụp dữ liệu tại một thời điểm. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Thread-safe**: An toàn với truy cập đồng thời. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Bootstrap**: Chuẩn bị dữ liệu khi startup. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra startup với database có/không có dữ liệu, concurrent update, UI refresh và restart host.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

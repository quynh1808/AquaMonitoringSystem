# DeviceControlOptions

Cấu hình này chỉ giữ master safety switch và command timeout. Endpoint Mitsubishi FX5U, handshake
và M/D mapping được Administrator nhập tại Network & Connections, lưu PostgreSQL. Tên `M100` được
adapter chuyển thành coil `0x2064`; admin không nhập địa chỉ decimal Modbus.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Cấu hình này chỉ giữ master safety switch và command timeout. Endpoint Mitsubishi FX5U, handshake và M/D mapping được Administrator nhập tại Network & Connections, lưu PostgreSQL. Tên `M100` được adapter chuyển thành coil `0x2064`; admin không nhập địa chỉ decimal Modbus.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **DeviceControlOptions**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Đọc khóa cấu hình dạng chuỗi rải rác dễ sai và cho phép production chạy với giá trị nguy hiểm.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Cung cấp strongly typed Options, validation và policy mode cho toàn host.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Program bind section vào class, đăng ký IValidateOptions và ValidateOnStart; service inject IOptions hoặc IOptionsMonitor.

- **Configuration/DeviceControlOptions.cs** (24 dòng): các điểm vào nhận diện được gồm DeviceControlOptions.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Program.cs, appsettings.json, Config/*.json, environment variables và service tiêu thụ Options.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Strongly typed Options**: Cấu hình được biểu diễn bằng class có kiểu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Validation**: Kiểm tra invariant trước khi sử dụng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Fail fast**: Dừng startup sớm khi cấu hình sai. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Operational mode**: Chế độ chọn nguồn dữ liệu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build và thử startup với cấu hình hợp lệ lẫn từng cấu hình không hợp lệ quan trọng.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

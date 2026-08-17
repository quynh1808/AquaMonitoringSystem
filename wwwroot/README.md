# Static assets trong wwwroot

## Mục đích

Thư mục này chứa file được ASP.NET Core phục vụ trực tiếp cho trình duyệt: stylesheet, JavaScript helper và favicon.

## Vai trò từng file

- app.css định nghĩa design token, layout responsive, card, form, trạng thái và breakpoint.
- app.js chứa helper trình duyệt tối thiểu cho chức năng Razor không thực hiện trực tiếp.
- favicon.svg là biểu tượng vector của tab.

## Ranh giới

CSS ẩn nút không phải authorization. JavaScript không được chứa secret hoặc quy tắc điều khiển có thẩm quyền. Browser có thể cache asset, nên khi kiểm tra release mới cần hard refresh và xác nhận đúng image SHA.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Thư mục này chứa file được ASP.NET Core phục vụ trực tiếp cho trình duyệt: stylesheet, JavaScript helper và favicon.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Static assets trong wwwroot**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Các file cấp repository định nghĩa cách toàn bộ solution được xây dựng, cấu hình, khởi động hoặc được tiếp cận. Sai khác tại đây có phạm vi ảnh hưởng toàn hệ thống.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Đóng vai trò điểm vào cho developer, công cụ build và application host.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Đối chiếu nội dung tài liệu với Program.cs, project/solution, appsettings, Compose và workflow. Giá trị production phải đến từ environment hoặc database động, không hard-code.

- Tài liệu này là runbook hoặc hồ sơ quyết định; source có thẩm quyền được liên kết trong nội dung và trong bản đồ toàn repository.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Program.cs, AquacultureMonitoring.csproj, appsettings.json, compose*.yaml, .github/workflows và các module con.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Repository**: Toàn bộ cây source được Git quản lý. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Composition root**: Nơi ghép dependency thành ứng dụng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Application host**: Tiến trình .NET chứa web và background service. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Immutable image**: Container image gắn đúng một bản build. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build solution ở Release, chạy test, validate Compose và kiểm tra không có secret hay output runtime trong diff.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

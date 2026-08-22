# Kiểm thử tự động

Project `AquacultureMonitoring.Tests` kiểm tra các vùng có rủi ro cao nhưng không cần PostgreSQL thật:

- chuyển đổi Mitsubishi M/D sang địa chỉ Modbus TCP;
- PBKDF2 và so sánh mật khẩu constant-time;
- phân quyền theo role/phạm vi ao tại service;
- validation của safety switch điều khiển thiết bị.
- mặc định sensor chưa publish trước commissioning.
- mặc định thiết bị ở Draft và trạng thái PendingRemoval tách biệt với Archived cho tới ACK.
- EF model có `latest_sensor_states`, không persist projection `TelemetryData`, có unique EventId,
  các cột metadata quan hệ và foreign key ao/sensor/vụ nuôi cốt lõi.

Chạy bằng `dotnet test Tests/AquacultureMonitoring.Tests.csproj`. Các integration test PostgreSQL/MQTT nên được bổ sung bằng container tạm trong CI trước khi bật phần cứng thật.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Project `AquacultureMonitoring.Tests` kiểm tra các vùng có rủi ro cao nhưng không cần PostgreSQL thật:

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Kiểm thử tự động**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Invariant tính toán, cấu hình và an toàn có thể bị phá khi source phát triển.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Là hàng rào regression tự động trước khi workflow publish container.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

MSTest project tham chiếu app, dùng test độc lập và chạy Release qua dotnet test.

- **Tests/CoreSafetyTests.cs** (173 dòng): các điểm vào nhận diện được gồm CoreSafetyTests.
- **Tests/PostgreSqlModelTests.cs** (50 dòng): kiểm tra invariant schema PostgreSQL mà không cần
  mở kết nối database.
- **Tests/packages.lock.json** (241 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

CoreSafetyTests.cs, project chính, package lock và workflow GitHub Actions.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Unit test**: Kiểm tra một đơn vị logic cô lập. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Regression**: Lỗi cũ quay lại. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Assertion**: Điều kiện test yêu cầu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Test fixture**: Dữ liệu/bối cảnh chuẩn bị cho test. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Chạy dotnet test Release và xác nhận test thực sự fail khi invariant tương ứng bị phá.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

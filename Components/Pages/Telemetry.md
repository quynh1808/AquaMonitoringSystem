# Telemetry page

## Mục đích

Trang `/telemetry` là màn hình quản trị cảm biến toàn hệ thống theo từng ao.

## Chức năng

- Hiển thị telemetry mới nhất của ao từ `FarmRuntimeState`.
- Vẽ trend từ `SensorReading` đã đi qua ingestion pipeline và được lưu MongoDB.
- Hiển thị inventory cảm biến: online/offline, data quality, last-seen, pin, RSSI, firmware, nguồn và hạn hiệu chuẩn.
- Mở `CalibrationModal` để ghi biên bản hiệu chuẩn.
- Hiển thị `MaintenanceTask` và lịch sử `CalibrationRecord` của cảm biến trong ao.
- Hiển thị sức khỏe của adapter simulation, MQTT, Modbus TCP và Modbus RTU.

## Cấu hình

Unit, số chữ số thập phân và các ngưỡng được đọc từ `Config/sensor-thresholds.json` qua `FarmStateService.GetThreshold`; trang không chứa số định mức cảm biến.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Trang `/telemetry` là màn hình quản trị cảm biến toàn hệ thống theo từng ao.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **Telemetry page**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Lớp UI cần trình bày trạng thái, validation và quyền nhất quán nhưng không được trở thành nơi duy nhất giữ quy tắc nghiệp vụ hoặc an toàn.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Biến state/view model thành giao diện Blazor và chuyển ý định người dùng tới service server-side.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Razor component nhận parameter hoặc inject service, xử lý event và re-render khi state thay đổi. Component có @page là route; component Shared được page tái sử dụng.

- **Components/Pages/Telemetry.razor** (210 dòng): các điểm vào nhận diện được gồm route /telemetry, inject FarmStateService State.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Services, Models, Runtime/FarmRuntimeState, Components/Routes.razor, layout, shared components và wwwroot/app.css.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Razor component**: File kết hợp HTML, directive và C#. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Route**: URL ánh xạ tới page bằng @page. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Render**: Quá trình tạo hoặc cập nhật DOM. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Circuit**: Kết nối tương tác giữa browser và Blazor Server. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **UI guard**: Điều kiện hiển thị, không thay authorization ở service. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build solution và kiểm tra route/component trên trình duyệt với trạng thái có dữ liệu, no-data, offline, đang Apply và thiếu quyền.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../../Docs/02.TECHNICAL-GLOSSARY.md)

# Phạm vi triển khai P0–P2

Tài liệu này ghi lại trạng thái đã triển khai sau khi phục hồi source, để lần sau có thể kiểm tra bằng code thay vì dựa vào lịch sử trao đổi.

## P0 — tính đúng, bảo mật và ranh giới production

- Cookie chứa `session_version`; server đối chiếu MongoDB trong `OnValidatePrincipal`. Khóa user, đổi quyền hoặc đổi/reset mật khẩu tăng version và thu hồi cookie cũ.
- Login/logout được audit; input credential bị giới hạn độ dài trước khi hash/query.
- Dashboard nhận danh sách ao được phép từ `FarmStateService`, không đọc toàn bộ runtime state.
- `OperationalModeGuard` cho phép simulation khi phát triển nhưng fail-fast nếu Production còn sensor/dashboard/cycle/device simulator, thiếu source thật hoặc tắt webhook cảnh báo.
- Mỗi ao chỉ có một `ProductionCycle.IsOpen=true` nhờ unique partial index. Service kiểm tra chuyển trạng thái, pond/cycle, thời gian, số lượng và tổng hao hụt + thu hoạch.
- FCR bằng tổng thức ăn chia tăng biomass ròng, với feed gắn trực tiếp vào `CycleId`. Các chỉ số được rebuild lúc bootstrap.

## P1 — telemetry và cảnh báo vận hành

- Envelope/reading hỗ trợ `EventId`, `SequenceNumber` và `IsBackfill`.
- Unique partial index chống trùng EventId. Sequence cũ bị bỏ, sequence gap đánh dấu `suspect`.
- Backfill bắt buộc có EventId, chỉ lưu history và không cập nhật live dashboard/automation.
- Timestamp tương lai/quá cũ được kiểm tra bằng `SystemBehavior`.
- Alert có new/acknowledged/resolved, occurrence count, người tiếp nhận/phụ trách, ghi chú xử lý và delivery log.
- Webhook dùng `IHttpClientFactory`, timeout riêng, không lưu query secret trong recipient audit.

## P2 — nghiệp vụ trang trại

- Kho vật tư theo category, lot, supplier, expiry, reorder level và transaction receipt/issue.
- Nhật ký sức khỏe: quan sát/chẩn đoán, điều trị, lô thuốc, xét nghiệm và withdrawal period.
- Nhật ký an toàn sinh học cho toàn trại hoặc từng ao.
- Work order có priority, assignee, due date, SOP và completion evidence.
- Chi phí liên kết ao/vụ; lô thu hoạch liên kết ao/vụ, khách hàng, vận chuyển và nhiệt độ bảo quản.
- Tất cả mutation kiểm quyền tại service, xác thực liên kết pond/cycle và ghi audit MongoDB.

## Kiểm tra hồi quy

```powershell
dotnet restore AquacultureMonitoring.sln --locked-mode
dotnet build AquacultureMonitoring.sln -c Release --no-restore -m:1
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build --no-restore
```

Các test lõi bao phủ password hashing, authorization theo ao, Modbus codec, device safety, FCR/số con còn sống và startup guard.

# FarmStateService

Facade scoped cho Blazor circuit, giữ UI tách khỏi MongoDB và adapter phần cứng.

## Trách nhiệm

- Đọc snapshot chung từ `FarmRuntimeState`.
- Nạp `ClaimsPrincipal`, lọc mọi projection theo phạm vi ao và giữ theme/ao đang chọn theo circuit.
- Thêm/xóa ao, thiết bị, feed log và xử lý cảnh báo.
- Gửi manual simulation dưới dạng `SensorEnvelope`; dữ liệu vẫn đi qua validation, MongoDB, alert và automation.
- Cung cấp trend từ dữ liệu đã ingest.
- Đọc threshold, unit, decimal places và health-score settings từ options.
- Các mutation gọi `FarmAccessService`; tên operator lấy từ claims thay vì dữ liệu form.
- `RecordCalibrationAsync`: lưu biên bản, cập nhật hạn tiếp theo, hoàn thành maintenance task, đóng cảnh báo và ghi audit log.

Các thay đổi UI được đưa vào `MongoWriteQueue` có backpressure để tránh mất write. Riêng device command đi trực tiếp qua `DeviceCommandService` vì intent phải durable trước dispatch. Khi tách API/backend, component có thể thay facade này bằng HTTP client dùng cùng DTO.

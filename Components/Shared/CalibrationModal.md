# CalibrationModal

## Mục đích

Modal cho phép kỹ thuật viên ghi nhận một lần hiệu chuẩn cảm biến ngay trong trang quan trắc.

## Dữ liệu nhập

- Người thực hiện.
- Giá trị trước hiệu chuẩn.
- Giá trị sau hiệu chuẩn.
- Ghi chú về dung dịch chuẩn, thiết bị đối chứng hoặc sai lệch.

## Kết quả

Khi lưu, component gọi `FarmStateService.RecordCalibration` để:

1. Ghi `CalibrationRecord` vào MongoDB.
2. Cập nhật ngày hiệu chuẩn và hạn tiếp theo của `SensorDefinition`.
3. Hoàn thành `MaintenanceTask` liên quan.
4. Đóng cảnh báo quá hạn/sắp đến hạn.
5. Ghi `AuditLog`.

## Tham số

- `IsOpen`: trạng thái hiển thị.
- `IsOpenChanged`: callback đóng/mở.
- `SensorId`: cảm biến cần hiệu chuẩn.

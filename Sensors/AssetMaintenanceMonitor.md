# AssetMaintenanceMonitor

## Mục đích

`AssetMaintenanceMonitor` là background service quản lý lịch hiệu chuẩn cảm biến. Service không thực hiện hiệu chuẩn vật lý mà tạo công việc bảo trì và cảnh báo để người vận hành xử lý đúng hạn.

## Luồng xử lý

1. Chờ `FarmRuntimeState` nạp xong dữ liệu MongoDB.
2. Định kỳ đọc `CalibrationDueDateUtc` của cảm biến đang bật.
3. Khi cảm biến bước vào khoảng nhắc trước hạn, tạo hoặc cập nhật `MaintenanceTask` có ID ổn định `calibration:{sensorId}`.
4. Tạo cảnh báo `warning`; nếu đã quá hạn thì dùng mức `critical`.
5. Ghi task và cảnh báo vào MongoDB.
6. Khi người dùng ghi nhận hiệu chuẩn trong UI, `FarmStateService.RecordCalibration` hoàn thành task và đóng cảnh báo liên quan.

## Cấu hình ngoài code

Các giá trị sau nằm trong `Config/system-behavior.json`:

- `DefaultCalibrationIntervalDays`: chu kỳ hiệu chuẩn mặc định.
- `CalibrationReminderDaysBefore`: số ngày nhắc trước hạn.
- `MaintenanceCheckIntervalMinutes`: chu kỳ chạy monitor.

## Thay thế bằng quy trình thực tế

Có thể nối thêm CMMS, email, LINE hoặc webhook ở bước tạo/cập nhật `MaintenanceTask`; không cần thay đổi pipeline cảm biến.

# SensorIngestionWorker

Pipeline trung tâm xử lý mọi bản tin cảm biến.

## Các bước

1. Kiểm tra `PondId`, `Metric` và lấy cấu hình metric.
2. Tạo hoặc cập nhật `SensorDefinition`.
3. Chuyển nguồn tự động khi cùng `SensorId` bắt đầu nhận dữ liệu MQTT/Modbus; cập nhật `ExternalAddress`. Bản tin simulation bị bỏ qua nếu sensor đang dùng nguồn thật, trừ khi cấu hình cho phép override.
4. Kiểm tra NaN, Infinity và miền `ValidMin`–`ValidMax`.
5. Tạo `SensorReading` append-only.
6. Cập nhật `telemetry_latest`, sensor state, runtime cache và source health.
7. Đóng cảnh báo mất kết nối khi sensor hoạt động lại.
8. Gọi data-quality alert, threshold alert và automation engine.

Ngưỡng và hành vi không ghi trực tiếp trong worker; chúng được đọc qua `IOptionsMonitor`.

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

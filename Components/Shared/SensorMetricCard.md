# SensorMetricCard

Component này bọc `MetricCard` nhưng lấy toàn bộ đơn vị, số chữ số, miền hợp lệ và vùng cảnh báo từ `Config/sensor-thresholds.json` thông qua `FarmStateService`.

Nhờ đó Dashboard và trang từng ao không chứa số ngưỡng trực tiếp trong Razor. Khi đổi giới hạn, tất cả card tự dùng cấu hình mới.

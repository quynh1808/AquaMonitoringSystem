# SensorIngestionWorker

Kiểm tra timestamp, metric, miền giá trị, EventId, sequence gap và backfill; chỉ cập nhật live state sau khi reading đã ghi MongoDB. Không có nhánh simulation override. Dữ liệu MQTT được xử lý như phép đo field, còn provenance nằm ở operational mode/firmware version.

# SensorMessageBus

Channel này tách tốc độ nhận dữ liệu khỏi tốc độ ghi MongoDB.

- MQTT/Modbus/simulation chỉ publish envelope.
- Một ingestion worker duy nhất validate và ghi dữ liệu theo đúng thứ tự nhận.
- Channel bị giới hạn 20.000 message để không tăng RAM vô hạn.
- Khi đầy, nguồn async sẽ chờ thay vì âm thầm bỏ dữ liệu.

Trong hệ thống lớn, module này có thể thay bằng Kafka, RabbitMQ hoặc MQTT broker nội bộ mà không đổi model dữ liệu.

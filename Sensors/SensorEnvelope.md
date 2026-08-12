# SensorEnvelope

DTO chuẩn giữa adapter và ingestion worker. `EventId` dùng idempotency, `SequenceNumber` phát hiện mất/thứ tự sai bản tin và `IsBackfill` tách dữ liệu lịch sử khỏi live state. Backfill bắt buộc có EventId.

`SensorEnvelope` là hợp đồng dữ liệu chung giữa mọi adapter và `SensorIngestionWorker`.

## Trường chính

- `SensorId`, `PondId`, `Metric`, `Value`, `Unit`.
- `TimestampUtc`, `Quality`.
- `SourceType`: simulation, MQTT, Modbus TCP hoặc Modbus RTU.
- `SourceAddress`: URI mô tả nguồn vật lý/logical, ví dụ topic MQTT hoặc register Modbus.
- `BatteryLevel`, `SignalStrength`, `FirmwareVersion`.
- `RawPayload`: payload gốc khi cần điều tra.

Nhờ hợp đồng này, UI và database không phụ thuộc giao thức phần cứng. Adapter mới chỉ cần chuyển dữ liệu của nó thành `SensorEnvelope`.

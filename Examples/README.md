# Ví dụ đưa dữ liệu thật vào pipeline

## MQTT payload

`mqtt-payload.json` dùng đúng cấu trúc `SensorEnvelope` mà server nhận. ESP32, Raspberry Pi hoặc gateway chỉ cần publish JSON tương tự vào topic khớp `TopicFilter` trong `Config/sensor-connections.json`.

## Thử bằng Mosquitto CLI

PowerShell:

```powershell
./Examples/publish-mqtt.ps1
```

Linux:

```bash
./Examples/publish-mqtt.sh
```

MQTT đã là nguồn mặc định P03. Đặt đúng host, username/password rồi publish vào topic `aquaculture/farm_main/pond_01/sensors/{metric}/telemetry`.

## Lưu ý timestamp

Gateway phải ưu tiên UTC ISO-8601 và gửi `eventId` cùng `sequenceNumber`. Server kiểm tra chéo pond/metric giữa payload và topic.

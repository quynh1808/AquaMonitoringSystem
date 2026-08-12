# Ví dụ đưa dữ liệu thật vào pipeline

## MQTT payload

`mqtt-payload.json` dùng đúng cấu trúc `SensorEnvelope` mà server nhận. Trước khi thử,
Administrator phải tạo broker, socket `sensor-node-01`, capability và sensor tương ứng trên web,
sau đó Apply thành công. Payload từ node chưa đăng ký hoặc sai socket/pond sẽ vào quarantine.

## Thử bằng Mosquitto CLI

PowerShell:

```powershell
./Examples/publish-mqtt.ps1
```

Linux:

```bash
./Examples/publish-mqtt.sh
```

Đặt đúng host, username/password rồi publish vào topic
`aquaculture/farm_main/pond_01/nodes/sensor-node-01/sensors/{sensorId}/telemetry`.

## Lưu ý timestamp

Gateway phải ưu tiên UTC ISO-8601 và gửi `eventId` cùng `sequenceNumber`. Server kiểm tra chéo pond/metric giữa payload và topic.

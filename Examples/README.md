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

Trước khi thử, đổi `ActiveSources` thành `["mqtt"]` hoặc `["simulation", "mqtt"]`, đặt `Mqtt.Enabled` thành `true`, rồi khởi động lại ứng dụng.

## Lưu ý timestamp

Gateway nên gửi UTC theo ISO-8601. Nếu `timestampUtc` bị bỏ trống, server dùng thời điểm nhận làm thời điểm đo.

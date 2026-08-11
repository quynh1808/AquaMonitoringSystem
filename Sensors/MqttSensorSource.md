# MqttSensorSource

MQTT adapter kết nối tới broker theo `Config/sensor-connections.json`, subscribe topic và chuyển JSON thành `SensorEnvelope` dùng chung. Mỗi connection có `SourceId` riêng để health monitoring xác định chính xác broker/adapter đang lỗi.

Payload chuẩn:

```json
{
  "sensorId": "sensor_pond_01_dissolvedOxygen",
  "pondId": "pond_01",
  "metric": "dissolvedOxygen",
  "value": 5.42,
  "unit": "mg/L",
  "timestampUtc": "2026-07-25T10:00:00Z",
  "quality": "good",
  "batteryLevel": 92,
  "signalStrength": -61,
  "firmwareVersion": "1.0.0"
}
```

Chức năng:

- username/password và TLS;
- topic wildcard;
- tự reconnect;
- bỏ qua và ghi log payload JSON lỗi mà không làm dừng toàn bộ adapter;
- publish dữ liệu vào cùng pipeline với simulation và Modbus.

Broker không nên mở trực tiếp ra Internet. Truy cập từ xa nên đi qua VPN hoặc MQTT TLS có xác thực và ACL.

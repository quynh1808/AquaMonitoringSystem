# Tham khảo cấu hình kết nối node cũ

Từ phiên bản cấu hình động, AquaOps **không đọc cấu hình node từ source code hoặc JSON**. Broker,
socket, ESP32 và PLC mapping được Administrator quản lý tại `Thiết lập hệ thống → Network & Connections`
và lưu trong MongoDB. Tài liệu này chỉ giữ lại cấu trúc cũ để đối chiếu khi migration; mọi secret đã bị loại bỏ.

## MQTT cũ (không còn được load)

```json
{
  "host": "mosquitto",
  "port": 1883,
  "clientId": "aquaops-server",
  "topicFilter": "aquaculture/+/+/sensors/+/telemetry",
  "username": "<redacted>",
  "password": "<redacted>",
  "useTls": false
}
```

Contract mới thêm node identity và desired/reported configuration:

```text
aquaculture/{farm}/{pond}/nodes/{node}/status
aquaculture/{farm}/{pond}/nodes/{node}/capabilities
aquaculture/{farm}/{pond}/nodes/{node}/config/request
aquaculture/{farm}/{pond}/nodes/{node}/config/desired
aquaculture/{farm}/{pond}/nodes/{node}/config/reported
aquaculture/{farm}/{pond}/nodes/{node}/sensors/{sensorId}/telemetry
aquaculture/{farm}/{pond}/nodes/{node}/sensors/{sensorId}/health
aquaculture/{farm}/{pond}/nodes/{node}/sync/data
aquaculture/{farm}/{pond}/nodes/{node}/sync/ack
```

## PLC FX5U cũ (không còn được load)

Endpoint cũ dùng `192.168.100.81:1502`, Unit ID 1, heartbeat D100. Mapping minh họa cũ:

```text
dev_01: command M100, feedback M101, fault M102
dev_02: command M200, feedback M201, fault M202
dev_03: command M300, feedback M301, fault M302
```

Mapping mới tách rõ request và feedback, nhập trên web theo chương trình PLC thực tế:

```text
M50   PLC config request
M51   Server apply request
M52   PLC apply acknowledgement
M53   Configuration error
D100  Heartbeat
D110  Desired configuration version
D111  Applied configuration version
D112  Configuration error code

M100  Manual START request (pulse)
M101  Manual STOP request (pulse)
M200  Physical/Local override feedback
M201  Remote control allowed
M210  Auto request
M211  Auto feedback
M300  Run feedback
M400  Fault feedback
```

Các địa chỉ trên chỉ là template cho ESP32 FX5U simulator, không phải địa chỉ bắt buộc của PLC thật.
Admin phải nhập đúng M/D variables đã lập trình trên FX5U và thực hiện commissioning trước khi bật
`HARDWARE_CONTROL_ENABLED=true`.

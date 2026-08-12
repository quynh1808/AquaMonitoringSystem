# MqttSensorSource

Subscribe `aquaculture/+/+/sensors/+/telemetry`, deserialize `SensorEnvelope` và kiểm tra PondId/Metric khớp topic trước khi đưa vào bounded bus. MQTT là pipeline chung cho tool simulation, ESP32 edge và probe thật.

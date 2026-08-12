# Aquaculture options

Các POCO này chỉ ánh xạ default cấp ứng dụng như `OperationalMode`, alert notification,
threshold mặc định, system behavior, seed và authentication. Broker, node socket, sensor channel
và PLC M/D mapping là cấu hình commissioning động trong MongoDB, không còn bind từ source JSON.
`OperationalMode` nhận `Simulation`, `EdgeSimulation`, `Production`; mọi mode đều dùng cùng
MQTT/Modbus contract và không bật generator trong server.

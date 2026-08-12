# Program.cs

Composition root đăng ký dynamic MQTT/Modbus supervisors đọc MongoDB, FX5U command transport,
authentication, authorization, health checks và Blazor. Legacy `SensorConnections` và node-specific
`DeviceControl:Fx5u` JSON không được load. Data Protection keys được lưu ngoài container để cookie,
antiforgery và secret connection mã hóa tồn tại qua deploy.

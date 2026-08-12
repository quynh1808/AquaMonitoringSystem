# Program.cs

Composition root đăng ký MQTT/Modbus sensor sources, FX5U command transport/PLC monitor, MongoDB, authentication, authorization, health checks và Blazor. Không đăng ký sensor generator, device ACK simulator hoặc production-cycle generator. Data Protection keys được lưu ngoài container để cookie/antiforgery tồn tại qua deploy.

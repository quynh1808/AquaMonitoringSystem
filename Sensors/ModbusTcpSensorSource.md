# ModbusTcpSensorSource

Mỗi connection cấu hình trong `sensor-connections.json` có host, port, thời gian poll và danh sách register mapping.

Adapter giữ TCP connection, đọc tuần tự từng mapping, kiểm tra transaction ID, function code và exception response rồi publish `SensorEnvelope`.

PLC/gateway nên cấp register map ổn định. Không để component Blazor tự đọc PLC vì sẽ tạo nhiều connection theo số người dùng.

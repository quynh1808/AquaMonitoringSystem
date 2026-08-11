# DataSourceHealthService

Quản lý trạng thái vận hành của từng nguồn dữ liệu:

- enabled/disabled;
- connected/disconnected;
- lần kết nối và bản tin gần nhất;
- tổng số bản tin;
- lỗi gần nhất.

Mỗi thay đổi được cập nhật vào `FarmRuntimeState` để UI thấy ngay và upsert vào MongoDB collection `data_source_health`.


## Định danh nguồn

Health được ghi theo `SensorEnvelope.SourceId`. MQTT và simulation dùng tên adapter; mỗi kết nối Modbus dùng ID riêng như `modbus-rtu:rs485-zone-b`, vì vậy số bản tin và lỗi không bị trộn giữa các gateway.

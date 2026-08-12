# FarmRepository

Repository gom các thao tác MongoDB mà UI và pipeline cảm biến cần dùng.

Nguyên tắc:

1. `sensor_readings` chỉ thêm mới để bảo toàn lịch sử.
2. `telemetry_latest` được upsert theo `PondId` để Dashboard không phải aggregate toàn bộ lịch sử.
3. Thông tin sensor/device/alert được upsert theo `Id`.
4. Xóa ao thực hiện xóa liên quan theo `PondId`.
5. Blazor component không gọi MongoDB Driver trực tiếp.
6. Device command được upsert theo `Id`: cùng một document đi từ `pending` sang trạng thái ACK cuối, hỗ trợ retry persistence mà không tạo duplicate.

Khi cần tách backend thành Web API, repository này có thể được chuyển nguyên sang project Infrastructure.

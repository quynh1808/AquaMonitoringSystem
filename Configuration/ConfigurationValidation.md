# ConfigurationValidation

## Mục đích

Kiểm tra các file cấu hình ngay khi ứng dụng khởi động. Ứng dụng dừng với thông báo rõ ràng thay vì chạy với ngưỡng hoặc mapping sai.

## Các nhóm kiểm tra

- Kết nối MongoDB và timeout.
- Catalog có đủ metric hiển thị và alarm defaults hợp lệ. Giới hạn vật lý runtime do edge capabilities cung cấp.
- Miền giá trị, bước thay đổi và xác suất bất thường của mô phỏng.
- Tên nguồn dữ liệu, MQTT host/port và mapping Modbus.
- Thời gian offline, retention, giới hạn runtime và dải điểm sức khỏe.

## Khi chỉnh JSON

Sau khi sửa file trong `Config/`, nên kiểm tra log. Một số giá trị dùng `IOptionsMonitor` và được nạp lại khi file thay đổi; việc bật/tắt cả một adapter kết nối cần khởi động lại ứng dụng để tạo hoặc dừng tiến trình nguồn dữ liệu.

# P03 — MQTT sensor edge và PLC FX5U-compatible

P03 loại bỏ toàn bộ generator sensor, dashboard data và device ACK simulation khỏi server. Giai đoạn sau P03 đã thay endpoint JSON bằng commissioning động; xem `Docs/DYNAMIC-COMMISSIONING.md`.

## Thành phần đã triển khai

- Một ao seed `pond_01`, không có sensor mặc định; admin chọn capability channel cần dùng.
- ESP32 sensor node nhận interval riêng từng sensor; interval 0 không đọc/gửi.
- ESP32 PLC simulator qua Wi-Fi, Modbus TCP FC01/FC03/FC05/FC06, config ACK và watchdog.
- Adapter `fx5u-modbus-tcp` ghi command coil rồi đọc feedback/fault trước khi hoàn tất command.
- PLC monitor đọc heartbeat và toàn bộ feedback mỗi 5 giây; ba lần lỗi liên tiếp đánh dấu thiết bị offline.
- Safety switch `HardwareControlEnabled=false` mặc định.
- Dashboard chỉ dùng runtime/MongoDB; dữ liệu chưa có nguồn thật hiển thị không có dữ liệu thay vì tạo số.

## Lưu ý database hiện có

`Config/seed-data.json` chỉ chạy khi collection `ponds` rỗng. P03 không tự xóa ao, history hoặc nghiệp vụ đã tồn tại vì đó là thao tác dữ liệu không thể phục hồi. Với database cũ nhiều ao, hãy backup trước, rồi archive/xóa dữ liệu ngoài `pond_01` theo quy trình được người quản trị phê duyệt.

## Thay phần edge bằng thiết bị thật

- Sensor: thay duy nhất hàm đọc giá trị trong firmware, giữ MQTT contract.
- PLC: tạo/chỉnh socket trên web, nhập M/D variables tương ứng logic/feedback/interlock trong GX Works3.
- Server, UI, audit, alert và automation không cần thay đổi.

Xem `Docs/OPERATING-MODES.md` để chuyển chế độ và checklist nghiệm thu.

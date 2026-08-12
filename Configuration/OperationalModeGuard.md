# OperationalModeGuard

Mọi profile P03 phải bật MQTT và dùng transport `fx5u-modbus-tcp`. Riêng `Production` còn bắt buộc hardware control và alert webhook đã bật. Guard fail-fast trước khi hosted services chạy; ba profile không tạo ba pipeline server khác nhau.

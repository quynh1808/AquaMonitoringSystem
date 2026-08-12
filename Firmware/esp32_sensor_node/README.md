# ESP32 sensor node

Firmware ESP-IDF cho WROOM-32E có sáu **capability channel**, nhưng tất cả mặc định interval `0`.
Chỉ channel được admin ánh xạ thành sensor và Apply/ACK thành công mới được đọc/gửi. Code hiện mô phỏng
giá trị tại edge; khi lắp probe thật, thay phần driver đọc channel và giữ nguyên protocol.

Tính năng: MQTT LWT/status, capabilities, desired/reported config, sensor health, NVS configuration,
EventId/sequence, NVS ring buffer và application-level sync ACK.

Build và provision: `Docs/ESP32-PROVISIONING.md`.

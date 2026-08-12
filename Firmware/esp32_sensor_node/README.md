# ESP32 sensor node

Firmware ESP-IDF cho WROOM-32E phát sáu metric của `pond_01` qua MQTT QoS 1 mỗi 30 giây. Trong P03, hàm `update_sensor_values()` tạo giá trị biến thiên tại edge; khi lắp probe thật, chỉ thay hàm này bằng driver ADC/RS-485 và giữ nguyên payload.

```powershell
idf.py set-target esp32
idf.py menuconfig
idf.py build
idf.py -p COM5 flash monitor
```

Không commit mật khẩu được sinh trong `sdkconfig`. Đồng hồ SNTP phải đồng bộ trước khi firmware publish để server không nhận timestamp sai.

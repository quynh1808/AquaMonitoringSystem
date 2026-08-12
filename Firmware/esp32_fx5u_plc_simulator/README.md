# ESP32 FX5U PLC simulator

Firmware ESP-IDF cho WROOM-32E mô phỏng đúng phần giao tiếp mà AquaOps cần: Modbus TCP FC01, FC03 và FC05, map FX5 `M0` tại coil `0x2000`, heartbeat `D100` và watchdog 15 giây.

```powershell
idf.py set-target esp32
idf.py menuconfig
idf.py build
idf.py -p COM6 flash monitor
```

Map mặc định:

| Thiết bị | Lệnh | Feedback | Fault |
|---|---:|---:|---:|
| Quạt nước | M100 | M101 | M102 |
| Máy cho ăn | M200 | M201 | M202 |
| Bơm | M300 | M301 | M302 |

Đây không phải trình giả lập đầy đủ Mitsubishi PLC. Khi chuyển sang FX5U thật, cấu hình cùng map trong GX Works3 và đổi endpoint trên server; không đổi contract AquaOps.

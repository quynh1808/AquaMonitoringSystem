# ESP32 FX5U-compatible PLC simulator

Firmware ESP-IDF cho WROOM-32E mô phỏng phần giao tiếp AquaOps cần: Modbus TCP FC01/FC03/FC05/FC06,
map FX5 `M0` tại coil `0x2000`, heartbeat, config version/ACK và watchdog. Đây không phải PLC emulator đầy đủ.

Map template:

| Chức năng | Địa chỉ |
|---|---:|
| Config request/apply/ACK/error | M50/M51/M52/M53 |
| Heartbeat/desired/applied/error code | D100/D110/D111/D112 |
| Remote START/STOP pulse | M100/M101 |
| Physical override/remote allowed | M200/M201 |
| Auto request/feedback | M210/M211 |
| Run feedback/fault | M300/M400 |

PLC thật có thể dùng địa chỉ khác; admin nhập đúng mapping trên giao diện. Provision/build xem
`Docs/ESP32-PROVISIONING.md`.

# Chuyển đổi Simulation, Edge Simulation và Production

## 1. Nguyên tắc chung

AquaOps không sinh telemetry và không giả ACK thiết bị. Cả ba chế độ đều dùng cùng hai contract:

```text
Nguồn cảm biến ── MQTT QoS 1 ──> Mosquitto ──> AquaOps
AquaOps ── Modbus TCP ──> PLC endpoint ── coil feedback/fault ──> AquaOps
```

`OperationalMode:Mode` chỉ khai báo nguồn tín hiệu và bật kiểm tra an toàn tương ứng. Chuyển chế độ không sửa source C# và không đổi `SensorId`, `PondId`, metric hay map PLC.

| Chế độ | Nguồn sensor | PLC endpoint | Mục đích |
|---|---|---|---|
| `Simulation` | Tool chạy trên máy tính publish MQTT | Modbus simulator trên máy tính/test bench | Test UI và server không cần ESP32 |
| `EdgeSimulation` | ESP32 tự tạo giá trị rồi publish MQTT | ESP32 WROOM-32E FX5U simulator qua Wi-Fi | Chế độ P03 hiện tại |
| `Production` | ESP32/gateway đọc probe thật | Mitsubishi FX5U thật | Vận hành thực tế sau commissioning |

Simulation ở đây là nguồn bên ngoài AquaOps. Không có generator simulation trong container ứng dụng.

## 2. Contract không thay đổi giữa các chế độ

Topic sensor:

```text
aquaculture/farm_main/pond_01/sensors/{metric}/telemetry
```

Các metric: `dissolvedOxygen`, `ph`, `temperature`, `salinity`, `ammonia`, `waterLevel`. Chu kỳ mặc định 30 giây; server đánh dấu offline sau 120 giây.

Map PLC:

| Thiết bị | Command | Feedback | Fault | Coil Modbus command |
|---|---:|---:|---:|---:|
| Quạt nước `dev_01` | M100 | M101 | M102 | `0x2064` |
| Máy cho ăn `dev_02` | M200 | M201 | M202 | `0x20C8` |
| Bơm `dev_03` | M300 | M301 | M302 | `0x212C` |

FX5 ánh xạ `M0` tại coil `0x2000`. `D100` là heartbeat mà server đọc mỗi 5 giây. Lệnh chỉ hoàn tất khi feedback đúng và fault đang OFF.

## 3. Chuẩn bị chung trên Ubuntu

Trong `.env`, broker phải bind vào IPv4 LAN để ESP32 kết nối được:

```dotenv
MQTT_BIND_ADDRESS=192.168.100.66
MQTT_USERNAME=aquaops_gateway
MQTT_PASSWORD='mat-khau-rieng'
```

Không dùng `0.0.0.0` nếu server có interface ngoài LAN. Không port-forward cổng `1883`, `1502`, `502` hoặc `8080` trên router.

Sau mỗi lần đổi cấu hình:

```bash
docker compose config --quiet
./scripts/deploy-ubuntu.sh "sha-<commit>"
docker compose ps
docker compose logs --tail=100 aquaops mosquitto
```

## 4. Chế độ Simulation

Thiết lập:

```dotenv
AQUAOPS_MODE=Simulation
HARDWARE_CONTROL_ENABLED=false
```

1. Chạy Mosquitto và AquaOps.
2. Publish payload thử bằng `Examples/publish-mqtt.ps1` hoặc `Examples/publish-mqtt.sh`.
3. Nếu cần thử điều khiển, chạy một Modbus TCP simulator bên ngoài với cùng register map và đặt `FX5U_HOST/PORT` trỏ tới simulator đó.
4. Chỉ đặt `HARDWARE_CONTROL_ENABLED=true` khi endpoint chắc chắn là test bench không nối tải.

Tiêu chí hoàn tất: sáu sensor nhận đúng topic, sequence/EventId không trùng, sensor offline đúng timeout và command có read-back.

## 5. Chuyển Simulation sang Edge Simulation

Flash hai project:

```powershell
Set-Location Firmware\esp32_sensor_node
idf.py set-target esp32
idf.py menuconfig
idf.py build
idf.py -p COM5 flash monitor

Set-Location ..\esp32_fx5u_plc_simulator
idf.py set-target esp32
idf.py menuconfig
idf.py build
idf.py -p COM6 flash monitor
```

Đặt DHCP reservation cho ESP32 PLC, ví dụ `192.168.100.81`, rồi sửa Ubuntu `.env`:

```dotenv
AQUAOPS_MODE=EdgeSimulation
FX5U_HOST=192.168.100.81
FX5U_PORT=1502
FX5U_UNIT_ID=1
HARDWARE_CONTROL_ENABLED=false
```

Kiểm tra trước khi mở command:

```bash
nc -vz 192.168.100.81 1502
docker compose logs --tail=100 aquaops
```

Sau khi xác nhận heartbeat `D100`, map coil, feedback, fault và watchdog trên test bench, đổi `HARDWARE_CONTROL_ENABLED=true` rồi deploy lại. ESP32 PLC simulator phải đưa `M100/M200/M300` về OFF khi server mất liên lạc quá 15 giây.

## 6. Chuyển Edge Simulation sang Production

Trước tiên giữ `HARDWARE_CONTROL_ENABLED=false` và thực hiện:

1. Thay hàm tạo giá trị trong sensor firmware bằng driver probe thật nhưng giữ nguyên MQTT payload.
2. Hiệu chuẩn từng probe, xác nhận unit, scale, timestamp và ngưỡng.
3. Trong GX Works3, cấu hình FX5U Modbus TCP và map `M100–M302`, `D100` đúng bảng trên.
4. Thay IP ESP32 PLC bằng IP FX5U thật; port thường là `502`, nhưng phải dùng đúng cấu hình PLC.
5. Test command trên bit/đèn test không nối tải; kiểm tra write ACK, feedback, fault và timeout.
6. Kiểm thử E-stop, manual override, overload, dry-run và mất mạng tại PLC/tủ điện.

`.env` production:

```dotenv
AQUAOPS_MODE=Production
FX5U_HOST=192.168.100.82
FX5U_PORT=502
FX5U_UNIT_ID=1
HARDWARE_CONTROL_ENABLED=true
ALERT_WEBHOOK_ENABLED=true
ALERT_WEBHOOK_URL=http://alert-receiver.lan/aquaops
COMPOSE_FILE=compose.yaml:compose.production.yaml
```

Startup guard sẽ từ chối Production nếu MQTT, hardware control hoặc kênh cảnh báo chưa được bật.

## 7. Rollback

Khi read-back sai, PLC mất heartbeat hoặc thiết bị vận hành bất thường:

1. Đưa tải về điều khiển local/manual tại tủ.
2. Đặt `HARDWARE_CONTROL_ENABLED=false` và deploy lại.
3. Không sửa trạng thái MongoDB để giả thiết bị đã OFF; xác nhận vật lý/feedback.
4. Nếu cần, trỏ `FX5U_HOST/PORT` về ESP32 test bench và đặt `AQUAOPS_MODE=EdgeSimulation`.
5. Lưu audit/log, xử lý nguyên nhân rồi thực hiện lại checklist commissioning.

Web application không thay thế safety PLC. E-stop, liên động, watchdog, bảo vệ quá tải và safe state phải hoạt động cục bộ dù AquaOps hoặc mạng LAN ngừng hoạt động.

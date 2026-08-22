# Runbook chạy ESP32 sensor node bằng PlatformIO

## 1. Mục đích và kết quả cuối cùng

Runbook này hướng dẫn toàn bộ vòng đời từ máy Windows chưa có PlatformIO đến khi ESP32-WROOM-32E:

1. build được firmware AquaOps;
2. được nạp qua USB;
3. nhận Wi-Fi, MQTT và identity qua UART;
4. kết nối broker Mosquitto trên Ubuntu Server 24.04 LTS trong LAN;
5. xuất hiện đúng socket trên AquaOps;
6. nhận chu kỳ từng sensor từ server;
7. gửi telemetry, health và replay dữ liệu sau mất kết nối.

Tài liệu dành cho firmware [esp32_sensor_node](README.md). Nó không áp dụng nguyên trạng cho
`esp32_fx5u_plc_simulator` vì simulator PLC chưa có environment PlatformIO riêng.

## 2. Kiến trúc cần hiểu trước khi thao tác

~~~text
Windows + PlatformIO                    Ubuntu Server 24.04 LTS
┌────────────────────┐                 ┌────────────────────────────┐
│ Build/flash ESP32  │                 │ Mosquitto :1883            │
│ Serial provisioning│                 │ AquaOps + PostgreSQL + Caddy  │
└─────────┬──────────┘                 └──────────────┬─────────────┘
          │ USB                                       │
          v                                           │ MQTT trong LAN
┌────────────────────┐                                │
│ ESP32-WROOM-32E    │<───────────────────────────────┘
│ node + 6 capability│
└────────────────────┘
~~~

ESP32 có sáu capability channel trong code nhưng interval của tất cả channel mặc định bằng 0.
Vì vậy sau khi chỉ flash và provision, node có thể online nhưng chưa gửi reading. Chỉ sensor được
Administrator thêm trên web và Apply/ACK với chu kỳ lớn hơn 0 mới hoạt động.

## 3. Điều kiện tiên quyết

### 3.1 Phần cứng

- ESP32 dùng module ESP32-WROOM-32E hoặc board tương thích profile `esp32dev`.
- Flash tối thiểu 4 MB vì custom partition kết thúc ở địa chỉ `0x400000`.
- Cáp USB có dây dữ liệu; nhiều cáp chỉ cấp nguồn và không tạo COM port.
- Nguồn USB ổn định. Wi-Fi có thể làm sụt áp trên board/cáp kém.
- Wi-Fi 2.4 GHz mà ESP32 truy cập được. ESP32-WROOM-32E không kết nối Wi-Fi 5 GHz.
- Máy Windows và ESP32 phải đi được đến IP LAN của Ubuntu Server.

### 3.2 Server AquaOps

Trước khi flash, xác nhận:

- Mosquitto đang healthy.
- Port MQTT 1883 được bind vào địa chỉ LAN của server, không chỉ `127.0.0.1`.
- Firewall cho phép ESP32 truy cập TCP 1883 từ VLAN/LAN thiết bị.
- Có username/password MQTT hợp lệ, ví dụ user `aquaops_gateway`.
- AquaOps đang dùng broker profile trỏ đến service Mosquitto.
- DNS và NTP khả dụng cho ESP32, hoặc LAN cho phép ESP32 truy cập Internet để đồng bộ
  `pool.ntp.org` qua UDP 123.

Lưu ý quan trọng: firmware hiện dùng SNTP `pool.ntp.org` và chỉ tạo record sau khi có UTC hợp lệ.
Nếu LAN hoàn toàn không có Internet/DNS/NTP, node có thể kết nối Wi-Fi/MQTT nhưng không gửi
telemetry. Khi vận hành mạng kín, cần triển khai NTP nội bộ và sửa firmware để dùng địa chỉ đó hoặc
làm NTP server có thể resolve theo tên hiện được cấu hình.

### 3.3 Phần mềm Windows

Khuyến nghị:

- Windows 10/11 64-bit.
- Git.
- Visual Studio Code.
- extension **PlatformIO IDE** của PlatformIO.
- driver USB-UART đúng chip trên board, thường là CP210x, CH340 hoặc FTDI.

Không bắt buộc cài ESP-IDF riêng nếu chỉ dùng PlatformIO. PlatformIO sẽ tải compiler, framework,
MQTT/cJSON managed components và tool cần thiết theo `platformio.ini`, `main/idf_component.yml` và
`dependencies.lock`. Lần build đầu tiên cần Internet và có thể mất nhiều phút.

## 4. Cài PlatformIO

### 4.1 Cách khuyến nghị: Visual Studio Code

1. Cài Visual Studio Code.
2. Mở tab Extensions bằng `Ctrl+Shift+X`.
3. Tìm **PlatformIO IDE** do PlatformIO phát hành.
4. Chọn Install.
5. Chờ PlatformIO Core được khởi tạo, sau đó restart Visual Studio Code nếu được yêu cầu.
6. Mở terminal mới trong Visual Studio Code và kiểm tra:

~~~powershell
pio --version
~~~

Nếu terminal thường của Windows không nhận `pio` nhưng PlatformIO toolbar hoạt động, mở terminal từ
PlatformIO IDE hoặc dùng biểu tượng PlatformIO ở thanh bên. Extension quản lý virtual environment
riêng và không phải lúc nào cũng thêm `pio` vào PATH hệ thống.

### 4.2 Cách CLI

Nếu đội vận hành muốn dùng CLI không phụ thuộc VS Code, cài PlatformIO Core theo hướng dẫn chính
thức. Sau khi cài, đóng và mở lại PowerShell rồi kiểm tra:

~~~powershell
pio --version
pio system info
~~~

Không cài nhiều bản PlatformIO Core bằng nhiều Python khác nhau nếu không cần. Khi có hai lệnh
`pio`, máy có thể build bằng package cache khác với VS Code và gây khó chẩn đoán.

## 5. Lấy source và mở đúng project

Ví dụ clone mới:

~~~powershell
git clone https://github.com/quynh1808/AquaMonitoringSystem.git
cd AquaMonitoringSystem/Firmware/esp32_sensor_node
code .
~~~

Nếu source đã có:

~~~powershell
cd C:/duong-dan/AquacultureMonitoringSystem/Firmware/esp32_sensor_node
git status --short
code .
~~~

Phải mở chính thư mục có `platformio.ini`. Nếu mở root repository, PlatformIO có thể không tự nhận
đây là một project hoặc chọn nhầm project khác. Đường dẫn ESP-IDF nên tránh ký tự lạ và khoảng trắng
để giảm lỗi từ CMake/toolchain cũ.

## 6. Kiểm tra source trước khi build

Trong terminal đặt tại `Firmware/esp32_sensor_node`:

~~~powershell
Get-Content platformio.ini
Get-Content main/idf_component.yml
Get-Content dependencies.lock
Get-Content sdkconfig.defaults
Get-Content partitions.csv
pio project config
~~~

Giá trị cần thấy:

- environment `aquaops_sensor_node`;
- platform `platformio/espressif32@7.0.1`;
- board `esp32dev`;
- framework `espidf`;
- source directory `main`;
- partition `partitions.csv`.
- managed MQTT `1.0.0` và cJSON `1.7.19~2`.

Không thêm Wi-Fi hoặc mật khẩu MQTT vào các file này.

## 7. Build firmware

### 7.1 Clean build đầu tiên

~~~powershell
pio run -t clean
pio run -e aquaops_sensor_node
~~~

Lần đầu PlatformIO tải toolchain và ESP-IDF. Không hủy giữa lúc đang giải nén package. Kết quả thành
công kết thúc bằng `SUCCESS` và tạo output dưới:

~~~text
.pio/build/aquaops_sensor_node/
~~~

Các artifact quan trọng thường gồm:

- `firmware.bin`: application binary;
- `firmware.elf`: binary có symbol phục vụ debug;
- `bootloader.bin`: bootloader;
- `partitions.bin`: partition table đã biên dịch.

Build đã được kiểm chứng trên PlatformIO Core 6.1.19 với Espressif32 7.0.1/ESP-IDF 6.0.1. Kết quả
tham chiếu tại thời điểm kiểm chứng: khoảng 37.8 KB RAM và 908 KB flash; số thực tế có thể thay đổi
khi source hoặc framework thay đổi.

Không copy riêng `firmware.bin` sang offset tùy ý. Lệnh upload của PlatformIO nạp đúng bootloader,
partition table và application vào các offset tương ứng.

### 7.2 Xem package thực tế đã dùng

~~~powershell
pio pkg list -e aquaops_sensor_node
pio system info
~~~

Lưu output này cùng biên bản release khi firmware dùng cho production. Nó giúp xác định đúng
toolchain/framework khi cần tái tạo lỗi.

### 7.3 Mở menuconfig khi thật sự cần

~~~powershell
pio run -e aquaops_sensor_node -t menuconfig
~~~

Sau khi thay đổi:

1. xác định option nào thực sự cần dùng chung;
2. đưa option đó vào `sdkconfig.defaults`;
3. không commit `sdkconfig.aquaops_sensor_node`;
4. clean build lại.

Không thay option bảo mật, flash size hoặc partition chỉ để làm mất một warning mà chưa hiểu tác
động đến thiết bị đã triển khai.

## 8. Xác định COM port

Cắm ESP32 rồi chạy:

~~~powershell
pio device list
~~~

Hoặc mở **Device Manager → Ports (COM & LPT)**. Rút/cắm lại board để xác định COM vừa xuất hiện.
Ví dụ các lệnh dưới dùng `COM5`; phải thay bằng port thật.

Nếu không có COM:

1. thử cáp dữ liệu khác;
2. thử cổng USB khác, tránh hub không cấp đủ nguồn;
3. kiểm tra chip USB-UART trên board;
4. cài driver từ nhà sản xuất chip/board;
5. kiểm tra Device Manager có Unknown device hay không.

## 9. Flash ESP32

Đóng mọi serial monitor đang giữ COM port, sau đó:

~~~powershell
pio run -e aquaops_sensor_node -t upload --upload-port COM5
~~~

Một số board tự điều khiển chân EN/BOOT. Nếu kết nối thất bại tại “Connecting…”:

1. giữ nút **BOOT**;
2. chạy lại lệnh upload;
3. khi log bắt đầu ghi flash thì thả BOOT;
4. nếu cần, nhấn nhanh **EN/RESET** trong lúc vẫn giữ BOOT.

Nếu upload không ổn định, sửa `upload_speed = 115200` trong `platformio.ini`, build/upload lại và
kiểm tra cáp/nguồn. Chỉ thay đổi này khi có bằng chứng upload lỗi ở tốc độ cao.

## 10. Mở serial monitor

~~~powershell
pio device monitor -p COM5 -b 115200
~~~

Hoặc build, upload và monitor liên tiếp:

~~~powershell
pio run -e aquaops_sensor_node -t upload --upload-port COM5
pio device monitor -p COM5 -b 115200
~~~

Thoát monitor bằng `Ctrl+C`. Nếu ký tự rác, xác nhận baud 115200 và nhấn EN/RESET. Nếu “port busy”,
đóng Arduino Serial Monitor, PuTTY, một cửa sổ VS Code khác hoặc tiến trình đang giữ COM.

## 11. Provision bootstrap bằng file hoặc UART

### 11.1 Cách khuyến nghị: file local và script tự động

Không cần mở Serial Monitor để nhập từng dòng:

~~~powershell
notepad .\provisioning.local.json
.\provision-esp32.ps1 -ValidateOnly
.\provision-esp32.ps1
~~~

Quy trình:

1. điền Wi-Fi, MQTT, Farm/Pond/Node ID trong `provisioning.local.json`;
2. chạy `-ValidateOnly` để kiểm JSON, byte length, URI và identity mà chưa mở COM;
3. kiểm tra đúng ESP32/COM rồi đổi `confirmProvisioning` thành `true`;
4. đóng Serial Monitor;
5. chạy script; script gửi toàn bộ khóa, status và restart;
6. mở monitor lại khi cần xác minh Wi-Fi/SNTP/MQTT.

File local chứa secret đã bị `.gitignore` chặn. Template commit được là
`provisioning.example.json`. Chi tiết về field, nhiều node, bảo mật, lỗi giữa chừng và cách phục hồi:
[PROVISIONING-FILE.md](PROVISIONING-FILE.md).

### 11.2 Cách dự phòng: nhập thủ công trong Serial Monitor

Firmware không chứa thông tin cụ thể của trang trại. Trong serial monitor, nhập lần lượt:

~~~text
set wifi_ssid TEN_WIFI_2_4_GHZ
set wifi_pass MAT_KHAU_WIFI
set mqtt_uri mqtt://192.168.100.66:1883
set mqtt_user aquaops_gateway
set mqtt_pass MAT_KHAU_MQTT
set farm_id farm_main
set pond_id pond_01
set node_id esp32-sensor-01
status
restart
~~~

Thay `192.168.100.66` bằng IP LAN tĩnh hoặc DHCP reservation của Ubuntu Server. Không dùng:

- `localhost` hoặc `127.0.0.1` vì trên ESP32 chúng chỉ chính ESP32;
- tên service Docker `mosquitto` vì tên đó chỉ resolve trong Docker network;
- IP thay đổi ngẫu nhiên theo DHCP nếu không có reservation.

Các identity phải khớp server:

- `farm_id` hiện phải là `farm_main` vì server dựng topic prefix theo giá trị này.
- `pond_id` phải là ID kỹ thuật của ao, ví dụ `pond_01`, không phải nhãn hiển thị “Ao số 1”.
- `node_id` phải duy nhất và phải trùng Node ID/Expected Client ID được khai báo trên web.

Lệnh `status` không echo secret. Sau `restart`, kiểm tra log theo thứ tự:

1. NVS khởi tạo;
2. bootstrap báo complete;
3. Wi-Fi nhận IP;
4. SNTP có UTC hợp lệ;
5. MQTT connected;
6. node publish status/capabilities;
7. node subscribe desired config và sync ACK.

Nếu bootstrap thiếu khóa, firmware ở trạng thái `INCOMPLETE` và không khởi động kết nối bình thường.

## 12. Kiểm tra MQTT từ Ubuntu Server

Trên server:

~~~bash
cd /duong-dan/toi/AquaMonitoringSystem
docker compose ps
docker compose logs --tail=200 mosquitto aquaops
~~~

Nếu image Mosquitto không chứa client tool ở host, có thể subscribe bên trong container khi tool có
sẵn:

~~~bash
docker compose exec mosquitto mosquitto_sub \
  -h 127.0.0.1 -p 1883 \
  -u aquaops_gateway -P 'MAT_KHAU_MQTT' \
  -t 'aquaculture/farm_main/pond_01/nodes/esp32-sensor-01/#' -v
~~~

Không đưa lệnh có password thật vào issue, ảnh chụp hoặc Git history. Với shell history nhạy cảm,
dùng file cấu hình client có permission chặt hoặc tài khoản test tạm thời rồi thu hồi.

Topic prefix mong đợi:

~~~text
aquaculture/farm_main/pond_01/nodes/esp32-sensor-01/
~~~

Các suffix phục vụ status, capabilities, desired/reported config, telemetry, health và sync phải
giữ đúng contract server. Không tự đổi topic trong firmware mà không cập nhật server và tài liệu.

## 13. Commissioning trên giao diện AquaOps

Đăng nhập bằng tài khoản Administrator.

### 13.1 Tạo broker profile

1. Vào **Thiết lập hệ thống → Network & Connections**.
2. Chọn **MQTT Broker**.
3. Với AquaOps chạy trong Docker Compose, host nội bộ của application thường là `mosquitto` và
   port `1883`.
4. Nhập credential đúng với broker.
5. Lưu và kiểm tra broker profile.

Địa chỉ broker trong web app và trên ESP32 có thể khác:

- AquaOps container dùng `mosquitto:1883` trong Docker network.
- ESP32 dùng `mqtt://IP_LAN_SERVER:1883`.

Hai địa chỉ đi đến cùng broker nhưng qua hai miền mạng khác nhau.

### 13.2 Tạo một socket cho node

1. Chọn **Thêm kết nối**.
2. Đặt tên dễ vận hành, ví dụ “ESP32 cảm biến Ao 1”.
3. Node type: ESP32.
4. Node ID: `esp32-sensor-01`.
5. Ao: `pond_01`.
6. Transport: MQTT.
7. Expected Client ID: khớp node đã provision.
8. Broker profile: profile vừa tạo.
9. Bật Enable.
10. Lưu Draft và xác nhận.

Một socket logic chỉ đại diện cho một node vật lý. Không dùng cùng Node ID cho hai ESP32.

### 13.3 Thêm sensor

Vào Ao 1 → Cảm biến → **Thêm cảm biến**. Chọn socket trên và một channel mà node công bố.

| Channel ID | Đại lượng dự kiến | Đơn vị thường dùng |
|---|---|---|
| `do-01` | Oxy hòa tan | mg/L |
| `ph-01` | pH | pH |
| `temperature-01` | Nhiệt độ nước | °C |
| `salinity-01` | Độ mặn | ppt |
| `ammonia-01` | Amoniac | mg/L |
| `water-level-01` | Mực nước | m |

Nhập Sensor ID duy nhất, tên hiển thị, metric, unit, ngưỡng cảnh báo và chu kỳ publish lớn hơn 0.
Không bắt buộc tạo đủ sáu sensor. Channel chưa được thêm hoặc có interval 0 sẽ không gửi dữ liệu.

Ngưỡng cảnh báo nhập trên server là ngưỡng vận hành của ao. Giới hạn vật lý, lỗi probe, CRC,
timeout và calibration health thuộc firmware/driver edge.

### 13.4 Apply và xác minh ACK

1. Trở lại **Network & Connections**.
2. Chọn **Apply**, nhập lý do và xác nhận.
3. Server chuyển deployment sang `Applying`.
4. ESP32 nhận desired version, lưu cấu hình, trả reported/ACK đúng CommandId và version.
5. Chỉ khi ACK hợp lệ, trạng thái mới thành `Applied`.

Nếu node offline, trạng thái có thể là `PendingReconnect`. Không sửa trực tiếp PostgreSQL để ép
`Applied` vì như vậy UI nói cấu hình đã chạy trong khi firmware chưa nhận.

## 14. Xác minh telemetry và health

Sau khi Apply:

1. Chờ ít nhất hai chu kỳ sensor dài nhất.
2. Mở trang Cảm biến của Ao 1.
3. Xác nhận chỉ sensor đã thêm mới có card.
4. Xác nhận timestamp tăng, giá trị và unit đúng.
5. Mở log kết nối để kiểm tra ACK/version.
6. Kiểm tra health không báo driver/physical range/time sync error.
7. Kiểm tra server không quarantine message vì sai SocketId/NodeId/ChannelId.

Không chỉ nhìn thấy “MQTT connected” rồi kết luận hệ thống hoàn chỉnh. Connected chỉ chứng minh
transport; Applied chứng minh cấu hình; telemetry/health chứng minh luồng nghiệp vụ.

## 15. Kiểm thử mất kết nối và đồng bộ bù

Thực hiện trên node test, không làm giữa ca vận hành.

### 15.1 Mất broker nhưng ESP32 vẫn có Wi-Fi

1. Ghi lại sequence cuối trên server.
2. Dừng hoặc chặn broker trong thời gian đủ sinh vài reading.
3. Xác nhận ESP32 tiếp tục lấy mẫu và đẩy record vào NVS ring buffer.
4. Khôi phục broker.
5. Xác nhận ESP32 reconnect, gửi record tồn và nhận application-level sync ACK.
6. Xác nhận EventId không trùng và server không tạo duplicate.

### 15.2 Mất Wi-Fi

1. Tắt access point test hoặc chặn node.
2. Xác nhận health/connection trên server chuyển offline sau timeout.
3. Khôi phục Wi-Fi.
4. Xác nhận node tự nhận IP, MQTT reconnect và replay queue.

### 15.3 Reboot trong lúc offline

1. Ngắt broker/Wi-Fi.
2. Chờ có record trong NVS.
3. Nhấn reset ESP32.
4. Khôi phục mạng.
5. Xác nhận record chưa ACK vẫn còn và được gửi bù.

### 15.4 Thay probe

1. Đưa socket/node vào trạng thái bảo trì theo quy trình vận hành.
2. Ngắt nguồn đúng cách và thay probe.
3. Khởi động lại, kiểm tra self-test/health và calibration.
4. Không đổi Sensor ID/Channel ID nếu vẫn là cùng vị trí đo logic.
5. Apply lại khi mapping/driver configuration thay đổi.
6. Chỉ cho phép automation dùng sensor sau khi health ổn định.

## 16. Cập nhật firmware

Quy trình cập nhật qua USB hiện tại:

~~~powershell
git pull --ff-only
cd Firmware/esp32_sensor_node
pio run -t clean
pio run -e aquaops_sensor_node
pio run -e aquaops_sensor_node -t upload --upload-port COM5
pio device monitor -p COM5 -b 115200
~~~

Flash application thông thường không xóa NVS, nhưng thay partition table, chạy erase hoặc thay đổi
schema NVS có thể làm mất bootstrap/offline data. Trước release:

- đọc release note;
- kiểm tra migration NVS;
- ghi lại identity node;
- thử trên một node staging;
- chuẩn bị binary/version rollback;
- không cập nhật đồng loạt tất cả ao trước khi node đầu tiên qua acceptance test.

Firmware hiện chưa có dual-slot OTA, do đó không quảng bá khả năng update/rollback từ xa như một
tính năng production.

## 17. Checklist nghiệm thu

- [ ] `pio run` kết thúc SUCCESS.
- [ ] Không có secret trong Git diff.
- [ ] Board có flash ít nhất 4 MB.
- [ ] Upload đúng COM và board boot được.
- [ ] `status` cho thấy bootstrap complete.
- [ ] ESP32 nhận IP Wi-Fi 2.4 GHz.
- [ ] UTC/SNTP hợp lệ.
- [ ] MQTT connected với đúng broker/user.
- [ ] Node ID, pond ID và topic prefix khớp socket.
- [ ] Capabilities xuất hiện.
- [ ] Chỉ sensor đã thêm có interval lớn hơn 0.
- [ ] Apply chuyển Applying → Applied bằng ACK thật.
- [ ] Telemetry và health xuất hiện trên server.
- [ ] Mất Wi-Fi và mất broker đều tự reconnect.
- [ ] Offline record được replay, ACK và không duplicate.
- [ ] Reboot không làm mất desired config đã applied.
- [ ] Log không lộ password.

## 18. Thuật ngữ

- **Build**: biên dịch source thành binary cho ESP32.
- **Flash/upload**: ghi bootloader, partition và application vào flash của board.
- **Serial monitor**: terminal UART dùng xem log và nhập lệnh provisioning.
- **Bootstrap**: cấu hình tối thiểu để node biết Wi-Fi, broker và identity.
- **Capability**: channel phần cứng/logic mà node có khả năng cung cấp.
- **Desired config**: cấu hình server muốn node chạy.
- **Reported config**: cấu hình node xác nhận đang chạy.
- **ACK**: phản hồi xác nhận gắn với command/version cụ thể.
- **Store-and-forward**: lưu record khi offline và gửi bù khi kết nối lại.
- **LWT**: Last Will and Testament; broker phát trạng thái offline khi client mất bất thường.
- **Quarantine**: giữ riêng message không khớp mapping hoặc contract, không đưa vào dữ liệu vận hành.
- **NTP/SNTP**: giao thức đồng bộ thời gian; timestamp đúng là điều kiện để xếp thứ tự và replay.

## 19. Đọc tiếp

- [Kiến trúc PlatformIO](../PLATFORMIO.md)
- [Xử lý lỗi PlatformIO/ESP32](PLATFORMIO-TROUBLESHOOTING.md)
- [Provisioning ESP32](../../Docs/13.ESP32-PROVISIONING.md)
- [Commissioning động](../../Docs/09.DYNAMIC-COMMISSIONING.md)
- [Chế độ Simulation/Edge/Production](../../Docs/11.OPERATING-MODES.md)
- [Tích hợp phần cứng](../../Docs/12.HARDWARE-INTEGRATION.md)
- [PlatformIO ESP-IDF chính thức](https://docs.platformio.org/en/latest/frameworks/espidf.html)

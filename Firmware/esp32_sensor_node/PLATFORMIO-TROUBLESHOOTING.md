# Xử lý lỗi PlatformIO và ESP32 sensor node

## 1. Mục đích

Tài liệu này chẩn đoán theo từng lớp: công cụ Windows → CMake/build → USB/flash → bootstrap →
Wi-Fi/time → MQTT → commissioning → telemetry/offline sync. Cần xác định lớp hỏng trước khi thay
cấu hình; xóa flash hoặc sửa source ngay từ đầu có thể làm mất bằng chứng và credential.

Mỗi lần xử lý nên ghi lại:

~~~powershell
pio --version
pio system info
pio pkg list -e aquaops_sensor_node
git status --short
pio device list
~~~

Đồng thời lưu toàn bộ lỗi đầu tiên, COM port, loại board, flash size, commit SHA, node ID, pond ID và
log cùng thời điểm từ Mosquitto/AquaOps. Không ghi password vào ticket hoặc ảnh chụp.

## 2. Sơ đồ khoanh vùng nhanh

~~~text
pio không chạy
  └─> cài đặt / PATH / permission

pio chạy nhưng build lỗi
  └─> platform package / CMake / component / Kconfig / partition / source

build thành công nhưng upload lỗi
  └─> COM / driver / cáp / BOOT / nguồn / upload speed

upload thành công nhưng node không chạy
  └─> serial baud / reset / boot loop / NVS / bootstrap

node có IP nhưng không có MQTT
  └─> broker LAN bind / firewall / URI / credential / ACL

MQTT connected nhưng không có dữ liệu
  └─> SNTP / interval 0 / mapping / Apply ACK / topic / quarantine

có dữ liệu nhưng mất hoặc trùng sau reconnect
  └─> NVS capacity / EventId / sequence / sync ACK / replay
~~~

## 3. Lỗi cài đặt và package

### 3.1 PowerShell báo `pio` không được nhận diện

Nguyên nhân:

- PlatformIO IDE chưa khởi tạo xong.
- Terminal được mở trước khi cài extension.
- Core nằm trong virtual environment của VS Code nhưng chưa có trong PATH hệ thống.
- Máy có nhiều Python/PlatformIO và đang gọi nhầm bản.

Cách xử lý:

1. Restart Visual Studio Code.
2. Mở PlatformIO Home để extension hoàn tất cài Core.
3. Mở terminal mới từ VS Code và chạy `pio --version`.
4. Nếu CLI ngoài VS Code chưa hoạt động, vẫn có thể dùng PlatformIO Project Tasks → Build.
5. Nếu cần CLI toàn hệ thống, cài PlatformIO Core theo tài liệu chính thức rồi dùng
   `Get-Command pio` để xác nhận đường dẫn.

Không copy executable từ thư mục PlatformIO sang System32.

### 3.2 `PermissionError` với `.platformio/platforms.lock` hoặc `packages.lock`

Nguyên nhân thường gặp:

- một tiến trình PlatformIO khác đang cài package;
- VS Code và PowerShell chạy bằng hai user khác nhau;
- thư mục PlatformIO từng được tạo bởi Administrator;
- antivirus hoặc backup đang giữ lock;
- quyền NTFS của thư mục cache bị sai.

Cách xử lý an toàn:

1. Đóng mọi cửa sổ VS Code/PlatformIO đang build.
2. Xác nhận không còn tiến trình PlatformIO/Python đang cài package.
3. Kiểm tra owner và quyền của thư mục PlatformIO thuộc đúng user hiện tại.
4. Mở lại VS Code bằng user bình thường; không luân phiên Run as administrator.
5. Chạy lại build.

Chỉ xóa lock khi chắc chắn không có package manager đang chạy. Xóa lock trong lúc tải có thể làm
cache hỏng.

### 3.3 Download toolchain đứng hoặc lỗi checksum

1. Kiểm tra Internet, proxy, VPN, ngày giờ Windows và dung lượng đĩa.
2. Đóng các build PlatformIO khác rồi thử lại.
3. Không bỏ version pin chỉ để né lỗi mạng.
4. Nếu package hỏng lặp lại, gỡ đúng package bằng PlatformIO package manager rồi build lại.
5. Trong mạng doanh nghiệp, cấu hình proxy và allow-list host package theo tài liệu PlatformIO.

## 4. Lỗi nhận project, CMake và component

### 4.1 `NotPlatformIOProjectError`

Terminal không đứng ở thư mục có `platformio.ini`:

~~~powershell
cd C:/duong-dan/AquacultureMonitoring20260812/Firmware/esp32_sensor_node
Test-Path platformio.ini
pio run -e aquaops_sensor_node
~~~

`Test-Path` phải trả về `True`.

### 4.2 Không tìm thấy `main` hoặc không có source

Kiểm tra:

~~~powershell
Select-String -Path platformio.ini -Pattern 'src_dir = main'
Test-Path main/sensor_node.c
Test-Path main/CMakeLists.txt
~~~

Không đổi tên `main` thành `src` nếu vẫn muốn giữ cấu trúc ESP-IDF hiện tại.

### 4.3 Không tìm thấy component `aqua_provisioning`

Triệu chứng có thể là unknown component trong `REQUIRES` hoặc không tìm thấy
`aqua_provisioning.h`. Kiểm tra:

~~~powershell
Test-Path ../components/aqua_provisioning/CMakeLists.txt
Test-Path ../components/aqua_provisioning/include/aqua_provisioning.h
Get-Content CMakeLists.txt
~~~

`CMakeLists.txt` gốc phải append `../components` vào `EXTRA_COMPONENT_DIRS` trước khi include
project ESP-IDF. Sau khi sửa:

~~~powershell
pio run -t clean
pio run -e aquaops_sensor_node
~~~

### 4.4 Header ESP-IDF không tìm thấy

Không thêm include path thủ công ngay. Kiểm tra `main/CMakeLists.txt` đã khai báo component chứa API
trong `REQUIRES` chưa. Source hiện cần Wi-Fi, event, netif, NVS, MQTT, JSON, lwIP và provisioning.

Nếu API thay đổi ở ESP-IDF mới:

1. đọc lỗi compile đầu tiên;
2. đối chiếu migration guide của đúng ESP-IDF được cài;
3. sửa source theo API chính thức hoặc pin platform đã kiểm thử;
4. clean build;
5. không dùng cast hoặc tắt warning để che lỗi lifecycle/ownership.

Nếu lỗi là `Failed to resolve component 'mqtt'` hoặc `'json'` với ESP-IDF 6:

1. xác nhận `main/idf_component.yml` tồn tại;
2. xác nhận manifest khai báo `espressif/mqtt` và `espressif/cjson`;
3. không thêm lại legacy `json` vào `REQUIRES`;
4. xác nhận `dependencies.lock` khớp target `esp32`;
5. cho phép Internet/registry ở lần tải đầu;
6. clean build; source tải về trong `managed_components`.

ESP-IDF 6 đã chuyển MQTT/cJSON khỏi framework core, nên đây không phải lỗi include path thông thường.

### 4.5 Option `CONFIG_AQUA_FLASH_BUFFER_CAPACITY` không tồn tại

~~~powershell
Test-Path main/Kconfig.projbuild
Get-Content main/Kconfig.projbuild
~~~

Nếu đổi tên symbol, phải sửa cả Kconfig và source. Không định nghĩa tay bằng build flag vì sẽ tạo
hai nguồn cấu hình.

### 4.6 Custom partition không được dùng hoặc app overflow

Kiểm tra đồng thời:

- `board_build.partitions = partitions.csv` trong `platformio.ini`;
- `CONFIG_PARTITION_TABLE_CUSTOM=y` trong `sdkconfig.defaults`;
- filename đúng chữ hoa/thường;
- build log tạo partition từ file project.

Partition cuối hiện kết thúc ở 4 MB. Nếu application overflow, tối ưu binary hoặc thiết kế lại
layout có tính toán. Không nới partition vượt flash vật lý.

### 4.7 Build cache gây kết quả khó hiểu

~~~powershell
pio run -t clean
pio run -e aquaops_sensor_node
~~~

Nếu vẫn lỗi và không có build đang chạy, có thể xóa riêng `.pio` của project rồi build lại. Không
xóa `sdkconfig.defaults`, `partitions.csv` hoặc NVS trên board.

## 5. Lỗi USB và flash

### 5.1 Không thấy COM port

- Dùng cáp USB có dữ liệu và thử cáp khác.
- Cắm trực tiếp vào máy, tránh hub yếu nguồn.
- Kiểm tra Device Manager.
- Cài driver đúng chip CP210x, CH340 hoặc FTDI trên board thật.
- Rút/cắm lại và so sánh output `pio device list`.
- Thử board/cáp trên máy khác để phân biệt lỗi Windows và lỗi phần cứng.

### 5.2 `Access is denied`, `port busy` hoặc không mở được COM

Đóng PlatformIO Serial Monitor khác, Arduino Serial Monitor, PuTTY/Tera Term, terminal đang chạy
`pio device monitor` và phần mềm vendor có thể giữ port. Không upload đồng thời từ toolbar và CLI.

### 5.3 Upload dừng ở `Connecting...`

1. Xác nhận đúng COM.
2. Giữ BOOT rồi nhấn nhanh EN/RESET.
3. Thả BOOT khi log bắt đầu ghi.
4. Giảm `upload_speed` xuống 115200.
5. Đổi cáp/cổng USB.
6. Ngắt ngoại vi đang kéo các strapping pin khi boot.

### 5.4 Timeout khi ghi, `Invalid head of packet` hoặc verify fail

Thường do cáp, nguồn, tốc độ upload hoặc board reset. Giảm upload speed, dùng cáp ngắn, tháo tải
ngoại vi tiêu thụ dòng và thử lại. Nếu luôn lỗi cùng địa chỉ, kiểm tra flash phần cứng.

### 5.5 Board boot loop sau flash

Mở monitor 115200 và lấy log từ lúc reset. Phân biệt:

- brownout: nguồn/cáp;
- partition hoặc image không hợp lệ: build/flash/layout;
- panic/backtrace: lỗi source;
- NVS init error: partition/schema;
- watchdog: task block hoặc thiếu stack.

Không erase flash ngay. Erase xóa bootstrap và offline records, làm mất trạng thái chẩn đoán.

## 6. Lỗi bootstrap và NVS

### 6.1 Log báo provisioning `INCOMPLETE`

Chạy `status` rồi nhập đủ:

~~~text
set wifi_ssid ...
set wifi_pass ...
set mqtt_uri mqtt://IP_LAN_SERVER:1883
set mqtt_user ...
set mqtt_pass ...
set farm_id farm_main
set pond_id pond_01
set node_id esp32-sensor-01
status
restart
~~~

Kiểm tra không có dấu cách thừa trong ID/URI. Secret không được echo là hành vi bảo mật có chủ đích.

### 6.2 Nhập UART command nhưng không có phản hồi

- Xác nhận baud 115200.
- Nhấn Enter để kết thúc dòng.
- Kiểm tra line ending của terminal.
- Không mở hai monitor cùng lúc.
- Reset rồi thử `status`.
- Kiểm tra firmware có chạy tới provisioning console.

### 6.3 Muốn xóa credential cũ

Erase NVS/flash là thao tác phá hủy. Trước khi thực hiện:

1. ghi lại node identity và mapping server;
2. xác nhận đúng board/COM;
3. chấp nhận offline record chưa sync sẽ mất;
4. chuẩn bị nhập lại bootstrap;
5. chỉ dùng target erase theo tài liệu PlatformIO/ESP-IDF khi được phê duyệt.

Không coi erase là bước sửa lỗi mặc định.

## 7. Lỗi Wi-Fi và thời gian

### 7.1 ESP32 không kết nối Wi-Fi

- WROOM-32E cần Wi-Fi 2.4 GHz.
- Kiểm tra SSID/password và ký tự đặc biệt nhập qua UART.
- Kiểm tra client isolation, MAC filtering, captive portal.
- Đưa board gần AP để loại trừ RSSI.
- Kiểm tra DHCP còn địa chỉ.
- Firmware hiện không mặc nhiên hỗ trợ mọi kiểu WPA Enterprise.

### 7.2 Có IP nhưng không có UTC/SNTP

Firmware hiện dùng `pool.ntp.org`. Kiểm tra:

- ESP32 có DNS;
- gateway cho phép UDP 123;
- LAN có Internet egress;
- firewall/VLAN không chặn;
- thời gian router/NTP upstream hợp lệ.

Trong mạng nội bộ kín, giải pháp production là NTP nội bộ và endpoint được cấu hình rõ ràng. Cho
đến khi source hỗ trợ NTP configurable, đây là dependency triển khai. Không tăng sensor interval
hoặc làm giả timestamp để né lỗi.

### 7.3 Wi-Fi reconnect liên tục

Kiểm tra RSSI, nguồn, AP roaming/band steering, DHCP lease và Wi-Fi reason code trong ESP-IDF log.
Wi-Fi flap sẽ kéo MQTT reconnect theo; cần sửa lớp Wi-Fi trước.

## 8. Lỗi MQTT

### 8.1 Connection refused hoặc timeout

Trên Ubuntu:

~~~bash
docker compose ps
docker compose logs --tail=200 mosquitto
sudo ss -lntp | grep ':1883'
~~~

MQTT bind address phải cho phép client LAN, không chỉ `127.0.0.1`. Kiểm tra firewall, VLAN và route
từ Wi-Fi thiết bị tới server.

### 8.2 `Not authorized`, `bad user name or password` hoặc `denied`

Phân biệt ba loại tài khoản:

- token GHCR chỉ dùng để pull image;
- MQTT user/password dùng Mosquitto;
- tài khoản web AquaOps dùng đăng nhập UI.

Password file Mosquitto phải có username hợp lệ trước dấu hai chấm, không phải dòng bắt đầu trực tiếp
bằng `:$7$...`. ACL phải cho phép node publish/subscribe đúng prefix. Không hiển thị secret trong log.

### 8.3 ESP32 dùng sai broker address

- ESP32 dùng `mqtt://IP_LAN_SERVER:1883`.
- AquaOps container thường dùng `mosquitto:1883`.
- `localhost` trên ESP32 là chính ESP32.
- Dùng DHCP reservation hoặc IP tĩnh cho server.

### 8.4 Connected rồi bị disconnect liên tục

Kiểm tra trùng MQTT Client ID giữa hai node, keepalive, Wi-Fi, broker restart, ACL và nguồn ESP32.
Broker thường ngắt client cũ khi client mới đăng nhập cùng Client ID. Mỗi node phải có ID duy nhất.

## 9. MQTT connected nhưng không có sensor data

### 9.1 Đây có thể là hành vi đúng: interval bằng 0

Firmware chủ ý không gửi channel chưa được cấu hình. Trên web:

1. tạo MQTT socket đúng pond/node;
2. thêm sensor với đúng Channel ID;
3. đặt Publish Interval lớn hơn 0;
4. Apply;
5. chờ trạng thái Applied;
6. chờ ít nhất hai chu kỳ.

Không hardcode interval trong source để làm card xuất hiện; cấu hình vận hành thuộc server.

### 9.2 Apply đứng ở `Applying` hoặc `PendingReconnect`

Kiểm tra theo thứ tự:

1. node online;
2. desired topic được publish;
3. node subscribe đúng prefix;
4. desired payload/version hợp lệ;
5. ESP32 lưu config;
6. ESP32 publish reported/ACK đúng CommandId và version;
7. server nhận ACK.

Không sửa trạng thái MongoDB bằng tay. Version hoặc CommandId không khớp phải bị từ chối để ACK cũ
không xác nhận nhầm cấu hình mới.

### 9.3 Dữ liệu bị quarantine

Đối chiếu:

- `farm_id = farm_main`;
- `pond_id` khớp ao của socket;
- `node_id` khớp Node ID socket;
- `channel_id` khớp sensor mapping.

Quarantine bảo vệ hệ thống khỏi dữ liệu của node/ao chưa khai báo. Không tắt validation để ép dữ
liệu xuất hiện.

### 9.4 SNTP chưa đồng bộ

Đây là nguyên nhân quan trọng trong LAN kín. Node có thể online và publish status nhưng không tạo
telemetry record vì chưa có UTC hợp lệ. Xử lý NTP/DNS trước khi nghi ngờ sensor mapping.

### 9.5 Transport connected nhưng sensor health lỗi

MQTT connected không có nghĩa probe healthy. Edge có thể báo timeout, CRC/protocol error, physical
range error, calibration invalid, stale sample hoặc driver init error. Giữ nguyên lỗi và hiển thị
trên server; không clamp giá trị sai thành giá trị đẹp vì automation có thể ra quyết định nguy hiểm.

## 10. Lỗi offline queue và đồng bộ bù

### 10.1 Mất dữ liệu khi offline lâu

Kiểm tra `CONFIG_AQUA_FLASH_BUFFER_CAPACITY`, số sensor đang bật, interval, thời gian offline, chính
sách ghi đè ring buffer và dung lượng NVS.

Ước lượng:

~~~text
số record tối thiểu =
  tổng (thời gian offline tối đa / interval của từng sensor)
  + health/config records
  + dự phòng vận hành
~~~

Nếu phải lưu lâu đáng kể, dùng storage phù hợp hơn thay vì tăng NVS vô hạn. Flash có chu kỳ ghi hữu
hạn và replay queue quá lớn cũng kéo dài thời gian phục hồi.

### 10.2 Dữ liệu trùng sau reconnect

EventId phải ổn định qua retry, sequence tăng đúng và server ingest phải idempotent. Record chỉ được
xóa sau application-level ACK đúng EventId/sequence và sau khi server persist. MQTT QoS ACK không
thay thế ACK nghiệp vụ.

### 10.3 Queue không giảm sau reconnect

Kiểm tra ESP32 có nhận sync ACK, ACK đúng EventId/sequence, topic đúng node prefix, server đã persist,
clock hợp lệ và reconnect không ngắt trước khi replay xong.

## 11. Thu thập log để báo lỗi

### 11.1 Windows

~~~powershell
git rev-parse HEAD
git status --short
pio --version
pio system info
pio pkg list -e aquaops_sensor_node
pio device list
pio run -e aquaops_sensor_node -v
~~~

### 11.2 Ubuntu

~~~bash
docker compose ps
docker compose logs --since=15m mosquitto aquaops
sudo ss -lntp | grep -E ':1883|:8080'
~~~

### 11.3 Thông tin tối thiểu trong ticket

- commit SHA;
- PlatformIO Core/platform/framework versions;
- board và USB-UART;
- lỗi đầu tiên;
- serial log từ boot;
- thời gian, node ID và pond ID;
- socket deployment state/version;
- broker/AquaOps log cùng khoảng thời gian;
- bước tái hiện;
- kết quả mong đợi và thực tế.

Rà soát log trước khi gửi để che password, token và dữ liệu định danh không cần thiết.

## 12. Những thao tác không nên làm khi chưa khoanh vùng

- Không erase flash/NVS.
- Không tắt authentication hoặc ACL Mosquitto.
- Không mở port 1883 ra Internet.
- Không commit SSID/password.
- Không sửa database thành Applied.
- Không tạo sáu sensor mặc định để che interval 0.
- Không đổi Node ID ngẫu nhiên.
- Không nâng/downgrade toolchain mà không lưu version cũ.
- Không tắt validation/quarantine.
- Không replay actuator command cũ sau reconnect.

## 13. Thuật ngữ

- **PATH**: danh sách thư mục Windows tìm executable như `pio`.
- **Lock file**: cờ đồng bộ ngăn hai package manager ghi cache cùng lúc.
- **Bootloader mode**: chế độ ESP32 chờ nạp firmware qua UART.
- **Brownout**: điện áp tụt dưới ngưỡng an toàn làm chip reset.
- **ACL**: danh sách quyền publish/subscribe theo user/topic.
- **Client ID**: định danh một MQTT connection; phải duy nhất với broker.
- **Idempotent ingest**: nhận lại cùng event không tạo thêm bản ghi trùng.
- **Quarantine**: giữ message không hợp lệ ngoài luồng dữ liệu vận hành.
- **Backtrace**: chuỗi địa chỉ/hàm tại thời điểm firmware panic.

## 14. Tài liệu liên quan

- [Runbook PlatformIO](PLATFORMIO-RUNBOOK.md)
- [Provision tự động bằng file JSON](PROVISIONING-FILE.md)
- [Kiến trúc PlatformIO](../PLATFORMIO.md)
- [Provisioning ESP32](../../Docs/13.ESP32-PROVISIONING.md)
- [Commissioning động](../../Docs/09.DYNAMIC-COMMISSIONING.md)
- [Troubleshooting toàn hệ thống](../../Docs/20.TROUBLESHOOTING.md)
- [PlatformIO FAQ chính thức](https://docs.platformio.org/en/latest/faq/index.html)

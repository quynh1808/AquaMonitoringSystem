# Kiến trúc build firmware ESP32 bằng PlatformIO

## 1. Mục đích của tài liệu

Tài liệu này giải thích cách PlatformIO được tích hợp vào firmware ESP32 sensor node của AquaOps,
ý nghĩa của từng file cấu hình và quan hệ giữa PlatformIO, ESP-IDF, CMake, Kconfig, partition table
và component dùng chung. Đây là tài liệu kiến trúc; quy trình thao tác từng bước nằm trong
[PLATFORMIO-RUNBOOK.md](esp32_sensor_node/PLATFORMIO-RUNBOOK.md), còn cách chẩn đoán lỗi nằm trong
[PLATFORMIO-TROUBLESHOOTING.md](esp32_sensor_node/PLATFORMIO-TROUBLESHOOTING.md).

Mục tiêu là để một người chưa cài ESP-IDF thủ công vẫn có thể:

1. Mở đúng thư mục firmware bằng Visual Studio Code.
2. Để PlatformIO tự cài đúng toolchain và framework.
3. Build, flash và theo dõi serial log bằng cùng một cấu hình đã commit.
4. Không đưa SSID, mật khẩu MQTT hoặc file build sinh tự động lên Git.
5. Hiểu file nào cần sửa khi thay board, partition hoặc component.

## 2. Vì sao AquaOps dùng PlatformIO nhưng vẫn giữ ESP-IDF

PlatformIO và ESP-IDF không thay thế lẫn nhau:

- **ESP-IDF** là SDK chính thức cung cấp FreeRTOS, Wi-Fi, MQTT, NVS, driver và API của ESP32.
- **PlatformIO Core** quản lý phiên bản compiler, Python tool, ESP-IDF, dependency, lệnh build,
  upload và serial monitor.
- **CMake** mô tả cấu trúc project và dependency giữa các component ESP-IDF.
- **Visual Studio Code PlatformIO IDE** là giao diện giúp chạy các lệnh PlatformIO.

Firmware vẫn là ứng dụng ESP-IDF thuần. Vì vậy code hiện có không phải chuyển sang Arduino,
không cần viết lại task FreeRTOS và vẫn có thể build bằng ESP-IDF CLI khi cần. PlatformIO chỉ tạo
một điểm vào nhất quán và giảm phụ thuộc vào cấu hình ESP-IDF riêng trên từng máy.

## 3. Phạm vi hiện được hỗ trợ

Cấu hình PlatformIO hiện áp dụng cho:

- `Firmware/esp32_sensor_node`: WROOM-32E đọc hoặc mô phỏng probe, nhận desired configuration,
  gửi telemetry/health và lưu dữ liệu offline trong NVS.

Project `Firmware/esp32_fx5u_plc_simulator` vẫn dùng ESP-IDF CLI cho đến khi có một
`platformio.ini` riêng và được build kiểm chứng. Không được chạy PlatformIO từ `Firmware` rồi giả
định cả hai firmware dùng chung một environment; mỗi firmware là một ứng dụng độc lập.

## 4. Cây file liên quan

~~~text
Firmware/
├── PLATFORMIO.md
├── components/
│   └── aqua_provisioning/
│       ├── CMakeLists.txt
│       ├── aqua_provisioning.c
│       └── include/aqua_provisioning.h
└── esp32_sensor_node/
    ├── .gitignore
    ├── CMakeLists.txt
    ├── dependencies.lock
    ├── platformio.ini
    ├── partitions.csv
    ├── provision-esp32.ps1
    ├── provisioning.example.json
    ├── provisioning.local.json  (Git ignore, chứa secret)
    ├── PROVISIONING-FILE.md
    ├── sdkconfig.defaults
    ├── PLATFORMIO-RUNBOOK.md
    ├── PLATFORMIO-TROUBLESHOOTING.md
    └── main/
        ├── CMakeLists.txt
        ├── idf_component.yml
        ├── Kconfig.projbuild
        └── sensor_node.c
~~~

Quan hệ build:

~~~text
platformio.ini
      │
      ├── chọn board esp32dev và framework espidf
      ├── chọn main/ làm source component
      └── chọn partitions.csv
              │
              v
CMakeLists.txt gốc ── thêm ../components ──> aqua_provisioning
      │
      v
main/CMakeLists.txt ── khai báo sensor_node.c và dependency ESP-IDF
      │
      ├── main/idf_component.yml ── tải MQTT + cJSON theo phiên bản pin
      ├── dependencies.lock ── khóa checksum dependency đã resolve
      ├── sdkconfig.defaults
      └── main/Kconfig.projbuild
              │
              v
       firmware.bin và firmware.elf trong .pio/build/
~~~

## 5. Giải thích `platformio.ini`

File [platformio.ini](esp32_sensor_node/platformio.ini) là nguồn cấu hình có thẩm quyền cho cách
PlatformIO build sensor node.

### 5.1 Khối `[platformio]`

~~~ini
[platformio]
default_envs = aquaops_sensor_node
src_dir = main
~~~

- `default_envs` xác định environment mặc định khi chạy `pio run` mà không truyền `-e`.
- `src_dir = main` rất quan trọng vì project ESP-IDF hiện hữu đặt application component trong
  thư mục `main`, không phải thư mục `src` mặc định của PlatformIO.
- Tên environment là định danh build, không phải `node_id` MQTT và không được dùng để phân biệt
  các ESP32 vật lý.

### 5.2 Khối `[env:aquaops_sensor_node]`

~~~ini
[env:aquaops_sensor_node]
platform = platformio/espressif32@7.0.1
board = esp32dev
framework = espidf
~~~

- `platform` được pin phiên bản để các máy không tự lấy platform mới hơn vào những thời điểm
  khác nhau. Platform package quyết định tập toolchain và ESP-IDF tương thích.
- `board = esp32dev` là board profile phổ dụng cho module ESP32 4 MB, phù hợp với WROOM-32E trong
  cấu hình hiện tại.
- `framework = espidf` yêu cầu build bằng ESP-IDF, không phải Arduino.

Platform `7.0.1` hiện resolve ESP-IDF `6.0.1`. Từ ESP-IDF 6, MQTT và built-in JSON không còn nằm sẵn
trong framework; chúng được khai báo trong manifest component và tải qua ESP-IDF Component Manager.

Nếu thay board thật, phải kiểm tra tối thiểu flash size, flash mode, PSRAM, clock và pin UART trước
khi đổi `board`. Không đổi board chỉ vì tên trên PCB không trùng `esp32dev`; profile là tập thông số
phần cứng, không phải nhãn thương mại.

### 5.3 Upload và serial monitor

~~~ini
monitor_speed = 115200
upload_speed = 460800
~~~

- `monitor_speed` phải trùng baud console của firmware. Nếu ký tự bị rác, kiểm tra giá trị này đầu
  tiên.
- `upload_speed` chỉ ảnh hưởng tốc độ nạp. Nếu nạp chập chờn do cáp dài hoặc USB-UART kém ổn định,
  giảm xuống `115200`; không cần thay `monitor_speed`.
- Không commit `upload_port` vì COM number khác nhau trên từng máy. Chỉ truyền port ở lệnh hoặc
  dùng cấu hình riêng không commit khi máy có nhiều board giống nhau.

### 5.4 Partition table

~~~ini
board_build.partitions = partitions.csv
~~~

Project dùng [partitions.csv](esp32_sensor_node/partitions.csv) thay vì layout mặc định:

- `nvs`: bắt đầu tại `0x9000`, dung lượng `0x100000`; lưu bootstrap, desired config, sequence và
  ring buffer dữ liệu offline.
- `phy_init`: dữ liệu hiệu chỉnh radio.
- `factory`: ứng dụng bắt đầu tại `0x110000`, dung lượng `0x2F0000`.

Partition cuối kết thúc ở `0x400000`, nghĩa là board phải có ít nhất 4 MB flash. Layout hiện chỉ có
một app `factory`, chưa có hai OTA slot. Muốn cập nhật firmware từ xa an toàn cần thiết kế lại
partition table thành `otadata` + `ota_0` + `ota_1`, tính lại kích thước NVS/app và kiểm thử rollback;
không chỉ thêm nút OTA trên giao diện.

## 6. Vai trò của CMake

### 6.1 `CMakeLists.txt` gốc

[CMakeLists.txt](esp32_sensor_node/CMakeLists.txt) thêm `Firmware/components` vào
`EXTRA_COMPONENT_DIRS` trước khi gọi project ESP-IDF. Nhờ đó cả PlatformIO lẫn `idf.py` đều tìm thấy
component `aqua_provisioning`.

Việc dùng `list(APPEND ...)` giữ lại các đường dẫn component khác mà PlatformIO hoặc ESP-IDF đã
thiết lập. Gán đè toàn bộ `EXTRA_COMPONENT_DIRS` có thể làm mất component do toolchain thêm vào.

### 6.2 `main/CMakeLists.txt`

File này đăng ký:

- source chính `sensor_node.c`;
- include directory tại `main`;
- các component bắt buộc: Wi-Fi, event loop, network interface, NVS, MQTT, lwIP và
  `aqua_provisioning`.

`REQUIRES` không chỉ giúp linker. Nó công bố dependency công khai của component để ESP-IDF sắp xếp
include path và thứ tự build. Khi source gọi API từ component ESP-IDF mới, cần cập nhật dependency
tại đây thay vì dựa vào include tình cờ từ component khác.

### 6.3 `main/idf_component.yml` và `dependencies.lock`

ESP-IDF 6 chuyển ESP-MQTT và cJSON sang ESP Component Registry:

- `main/idf_component.yml` khai báo ESP-IDF 6.0.x, `espressif/mqtt 1.0.0` và
  `espressif/cjson 1.7.19~2`.
- `dependencies.lock` ghi phiên bản, component hash, registry source và target đã resolve.
- `managed_components/` là source được tải về theo manifest/lock và không commit.

Manifest thể hiện dependency trực tiếp do lập trình viên lựa chọn; lock file đảm bảo lần build sau
dùng đúng dependency đã resolve. Khi nâng MQTT/cJSON, sửa manifest có chủ đích, build online để tạo
lock mới, review cả hai file và chạy lại test mất kết nối/replay.

## 7. `sdkconfig.defaults`, `sdkconfig.*` và Kconfig

### 7.1 `sdkconfig.defaults`

[sdkconfig.defaults](esp32_sensor_node/sdkconfig.defaults) là tập giá trị mặc định đã được review và
được commit. Hiện nó:

- tăng stack cho main task;
- bật MQTT 3.1.1;
- giới hạn số NTP server của lwIP;
- chọn custom partition table.

File này không nên chứa mật khẩu Wi-Fi/MQTT. Bootstrap runtime được nhập qua UART và lưu trong NVS.

### 7.2 `sdkconfig.aquaops_sensor_node`

Khi build, PlatformIO có thể tạo file `sdkconfig.<environment>`. Đây là cấu hình đã merge theo máy
và phiên bản framework, nên bị bỏ qua bởi `.gitignore`. Nếu thay đổi trong `menuconfig` cần được áp
dụng cho cả nhóm, hãy chép đúng option cần thiết sang `sdkconfig.defaults`, xóa build cũ rồi build
lại để xác minh.

### 7.3 `main/Kconfig.projbuild`

Kconfig riêng khai báo `CONFIG_AQUA_FLASH_BUFFER_CAPACITY`, giới hạn từ 32 đến 512 và mặc định 128.
Giá trị này kiểm soát số record tối đa của hàng đợi store-and-forward trong NVS.

Tăng dung lượng không đồng nghĩa có thể lưu vô hạn:

- record lớn hơn làm tăng flash write và thời gian replay;
- NVS vẫn có dung lượng hữu hạn;
- flash có chu kỳ ghi hữu hạn;
- cần đo tốc độ sinh dữ liệu khi server offline lâu nhất dự kiến.

## 8. Thư mục `.pio`

PlatformIO tạo `.pio` để chứa:

- tool metadata của project;
- object files;
- `firmware.elf`;
- `firmware.bin`;
- partition binary;
- các file trung gian.

ESP-IDF Component Manager còn tạo `managed_components/` để chứa source dependency tải từ registry.
Cả hai thư mục đều có thể tái tạo; `dependencies.lock` mới là file cần commit.

Đây là output có thể tái tạo, phụ thuộc máy và thường rất lớn, vì vậy không được commit. Khi nghi ngờ
cache build lỗi, chạy `pio run -t clean`; chỉ xóa `.pio` khi clean không đủ và không có tiến trình
PlatformIO nào đang chạy.

## 9. Vai trò của `.gitignore`

[.gitignore](esp32_sensor_node/.gitignore) loại trừ:

- `.pio/`: artifact và cache của PlatformIO;
- `managed_components/`: source MQTT/cJSON tải từ ESP Component Registry;
- `sdkconfig.*`: cấu hình sinh theo environment;
- `provisioning*.local.json`: Wi-Fi/MQTT credential và identity của node trên máy kỹ thuật viên;
- ngoại lệ `sdkconfig.defaults`: baseline có chủ đích phải được giữ trong Git.

Vì `.gitignore` nằm ngay trong `esp32_sensor_node`, pattern phải tương đối với thư mục này. Viết
`Firmware/**/.pio/` tại đây sẽ không khớp như mong muốn; pattern đó chỉ phù hợp nếu đặt ở root Git.

## 10. Cấu hình nào được commit, cấu hình nào chỉ tồn tại runtime

| Loại | Ví dụ | Nơi lưu | Commit? |
|---|---|---|---|
| Build reproducible | platform version, board, partition | `platformio.ini` | Có |
| Managed dependency intent | MQTT/cJSON constraints | `main/idf_component.yml` | Có |
| Resolved dependency | version, hash, target | `dependencies.lock` | Có |
| Firmware defaults | stack, MQTT protocol | `sdkconfig.defaults` | Có |
| Capability code | channel id, driver mapping | `sensor_node.c` | Có |
| Bootstrap identity | Wi-Fi, broker URI, farm/pond/node ID | NVS `aqua_boot` | Không |
| MQTT secret | username/password | NVS `aqua_boot` | Không |
| Provision input local | Wi-Fi/MQTT/identity trước khi ghi NVS | `provisioning*.local.json` | Không |
| Provision template/script | schema mẫu và công cụ UART tự động | example JSON + PowerShell | Có |
| Desired sensor config | interval, server config version | NVS/runtime qua MQTT | Không |
| Build artifact | `.bin`, `.elf`, object | `.pio/` | Không |

Ranh giới này giúp cùng một binary có thể chạy trên nhiều node. Không tạo một firmware riêng chứa
SSID cho từng ao; việc đó làm lộ secret, khó thay mạng và phá khả năng quản lý tập trung.

## 11. Tái lập build và quản lý phiên bản

Để build có thể tái lập:

1. Commit `platformio.ini`, CMake, manifest/lock, `sdkconfig.defaults` và partition table trong cùng thay đổi.
2. Pin PlatformIO platform; chỉ nâng phiên bản trong pull request riêng.
3. Chạy clean build sau khi nâng platform/framework.
4. Ghi lại `pio --version` và `pio pkg list` trong bằng chứng kiểm thử.
5. Không commit `.pio` hoặc `managed_components`; toolchain/dependency phải được khôi phục từ cấu hình.
6. Build cả firmware và server khi thay MQTT contract.

Pin platform giúp giảm drift nhưng chưa tạo binary tuyệt đối giống nhau ở mọi thời điểm nếu package
phụ thuộc bên trong thay đổi. Với release production, nên lưu checksum artifact và công bố artifact
firmware từ CI thay vì yêu cầu mọi kỹ thuật viên tự build.

## 12. So sánh lệnh PlatformIO và ESP-IDF CLI

| Mục tiêu | PlatformIO | ESP-IDF CLI |
|---|---|---|
| Build | `pio run -e aquaops_sensor_node` | `idf.py build` |
| Clean | `pio run -t clean` | `idf.py fullclean` |
| Menuconfig | `pio run -t menuconfig` | `idf.py menuconfig` |
| Flash | `pio run -t upload --upload-port COM5` | `idf.py -p COM5 flash` |
| Monitor | `pio device monitor -p COM5 -b 115200` | `idf.py -p COM5 monitor` |

Không trộn output directory của hai hệ thống khi chẩn đoán. PlatformIO dùng `.pio`; ESP-IDF CLI
thường dùng `build`. Nếu vừa đổi `sdkconfig` bằng một tool rồi build bằng tool còn lại, phải chắc
chắn option đã được đưa về `sdkconfig.defaults`.

## 13. Thay cảm biến mô phỏng bằng probe thật

PlatformIO không thay đổi kiến trúc runtime. Khi thay simulation:

1. Giữ nguyên `channel_id`, schema MQTT, EventId, sequence, health và offline queue.
2. Tách driver probe thành component riêng nếu driver có I2C/UART/Modbus và state machine đáng kể.
3. Chuyển hàm tạo giá trị mô phỏng sang lời gọi driver.
4. Đưa giới hạn vật lý cố định, lỗi CRC, timeout và lỗi hiệu chỉnh thành health code từ edge.
5. Không đưa Wi-Fi/MQTT secret vào driver.
6. Build, flash và kiểm thử mất probe, mất Wi-Fi, mất broker, reboot và replay.

Nhờ giữ contract, server không cần biết giá trị đến từ mô phỏng hay probe vật lý; server chỉ xử lý
telemetry và health đã chuẩn hóa.

## 14. Thứ tự tài liệu đề nghị

1. Tài liệu hiện tại để hiểu kiến trúc build.
2. [PLATFORMIO-RUNBOOK.md](esp32_sensor_node/PLATFORMIO-RUNBOOK.md) để cài, build, flash và
   commissioning.
3. [PROVISIONING-FILE.md](esp32_sensor_node/PROVISIONING-FILE.md) để nhập một file và tự gửi qua UART.
4. [ESP32 provisioning](../Docs/13.ESP32-PROVISIONING.md) để hiểu khóa NVS và lệnh UART.
5. [Dynamic commissioning](../Docs/09.DYNAMIC-COMMISSIONING.md) để hiểu desired/reported/ACK.
6. [Operating modes](../Docs/11.OPERATING-MODES.md) trước khi chuyển từ edge simulation sang probe.
7. [PLATFORMIO-TROUBLESHOOTING.md](esp32_sensor_node/PLATFORMIO-TROUBLESHOOTING.md) khi kết quả
   khác mong đợi.

## 15. Thuật ngữ

- **Environment**: một bộ cấu hình PlatformIO có tên, gồm board, framework và tùy chọn build.
- **Toolchain**: compiler, linker và công cụ tạo binary cho kiến trúc Xtensa ESP32.
- **Framework**: SDK cung cấp API và runtime; ở đây là ESP-IDF.
- **Component**: đơn vị module của ESP-IDF có source, include và CMake riêng.
- **Managed component**: component được tải từ ESP Component Registry theo manifest.
- **Dependency lock**: snapshot phiên bản và hash đã resolve để build sau dùng cùng input.
- **Partition table**: bản đồ chia flash thành NVS, app, OTA hoặc filesystem.
- **Kconfig**: hệ thống khai báo và chọn compile-time option của ESP-IDF.
- **NVS**: key-value storage bền vững trên flash.
- **Artifact**: kết quả build như `.bin`, `.elf`, map file.
- **Provisioning**: đưa bootstrap identity và credential vào thiết bị lần đầu.
- **File-assisted provisioning**: script đọc secret từ file local và tự gửi UART thay vì nhập tay.
- **Commissioning**: đăng ký node/cảm biến với server và áp cấu hình vận hành có xác nhận.

## 16. Tài liệu chính thức

- [PlatformIO: ESP-IDF framework](https://docs.platformio.org/en/latest/frameworks/espidf.html)
- [PlatformIO: Espressif 32 platform](https://docs.platformio.org/en/latest/platforms/espressif32.html)
- [PlatformIO: Espressif ESP32 Dev Module](https://docs.platformio.org/en/stable/boards/espressif32/esp32dev.html)
- [PlatformIO: build, upload và monitor ESP-IDF](https://docs.platformio.org/en/latest/tutorials/espressif32/espidf_debugging_unit_testing_analysis.html)
- [Espressif: partition tables](https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-guides/partition-tables.html)
- [Espressif: IDF Component Manager](https://docs.espressif.com/projects/esp-idf/en/stable/esp32c6/api-guides/tools/idf-component-manager.html)
- [Espressif: MQTT và cJSON migration trong ESP-IDF 6](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/migration-guides/release-6.x/6.0/protocols.html)

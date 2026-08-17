# Provision ESP32 bằng file JSON, không nhập từng lệnh trong Serial Monitor

## 1. Mục đích

Tài liệu này hướng dẫn cấu hình Wi-Fi, MQTT và identity cho ESP32 sensor node bằng một file JSON cục
bộ. Script PowerShell đọc file, kiểm tra dữ liệu rồi tự gửi toàn bộ lệnh provisioning qua COM port.
Người dùng không cần mở Serial Monitor và gõ từng dòng.

Giải pháp vẫn dùng provisioning API/NVS hiện có của firmware, vì vậy:

- secret không được hardcode trong `sensor_node.c`;
- cùng một firmware binary dùng được cho nhiều ESP32;
- thay Wi-Fi hoặc MQTT không cần build lại firmware;
- server contract, desired/reported config và sensor interval không thay đổi;
- file chứa mật khẩu được Git bỏ qua.

Đây là **file-assisted UART provisioning**: thao tác được tự động hóa nhưng máy Windows vẫn phải nối
USB/UART với ESP32. Nó không phải provisioning qua Wi-Fi, Bluetooth hoặc Internet.

## 2. Các file và vai trò

| File | Vai trò | Có commit? |
|---|---|---|
| `provisioning.example.json` | Mẫu cấu trúc, placeholder và giá trị identity tham khảo. | Có |
| `provisioning.local.json` | Giá trị thật của một ESP32 trên máy hiện tại. | Không |
| `provision-esp32.ps1` | Validation, mở COM, gửi lệnh, đọc phản hồi đã che secret và restart. | Có |
| `.gitignore` | Chặn mọi file `provisioning*.local.json` khỏi Git. | Có |
| `aqua_provisioning.c` | Firmware nhận `set/status/restart` và persist vào NVS `aqua_boot`. | Có |

Tách example và local là bắt buộc. Nếu điền mật khẩu thật trực tiếp vào example, Git sẽ theo dõi và
secret có thể tồn tại vĩnh viễn trong lịch sử commit dù sau đó đã xóa khỏi file hiện tại.

## 3. Luồng hoạt động

~~~text
provisioning.local.json
          │
          v
provision-esp32.ps1
  ├─ kiểm JSON, byte length, URI, ID, COM
  ├─ chặn placeholder
  ├─ yêu cầu confirmProvisioning=true
  └─ gửi từng dòng qua UART 115200
          │
          v
aqua_provisioning console task
  ├─ nvs_set_str
  ├─ nvs_commit từng khóa
  ├─ status
  └─ restart
          │
          v
NVS namespace aqua_boot
  └─ app_main đọc cấu hình mới khi boot
~~~

Script không đọc/ghi trực tiếp flash bằng offset và không tạo firmware riêng. Nó sử dụng đúng parser
UART đã có trong component provisioning, nên validation và key name của firmware vẫn là nguồn có
thẩm quyền.

## 4. Chuẩn bị

### 4.1 Điều kiện

- Firmware sensor node đã được flash.
- ESP32 nối với Windows bằng cáp USB dữ liệu.
- Driver CP210x/CH340/FTDI đúng với board.
- Không có PlatformIO Serial Monitor, Arduino Monitor hoặc PuTTY đang giữ COM port.
- PowerShell chạy bằng chính user đang dùng VS Code.

### 4.2 Xác định COM port

Từ thư mục `Firmware/esp32_sensor_node`:

~~~powershell
pio device list
~~~

Ví dụ kết quả là `COM5`. Rút/cắm board để phân biệt nếu máy có nhiều thiết bị serial. PlatformIO
chính thức cũng dùng `pio device list` để liệt kê port khả dụng.

### 4.3 Tạo file local trên một clone mới

Repository hiện có template. Sau khi clone ở máy mới:

~~~powershell
Copy-Item provisioning.example.json provisioning.local.json
notepad provisioning.local.json
~~~

Trong workspace hiện tại, `provisioning.local.json` đã được tạo với placeholder để bạn điền.

## 5. Cấu trúc file JSON

~~~json
{
  "_comment": "LOCAL SECRET FILE - DO NOT COMMIT.",
  "confirmProvisioning": false,
  "serialPort": "COM5",
  "baudRate": 115200,
  "wifiSsid": "THAY_TEN_WIFI_2_4_GHZ",
  "wifiPassword": "THAY_MAT_KHAU_WIFI",
  "mqttUri": "mqtt://192.168.100.66:1883",
  "mqttUsername": "aquaops_gateway",
  "mqttPassword": "THAY_MAT_KHAU_MQTT",
  "farmId": "farm_main",
  "pondId": "pond_01",
  "nodeId": "esp32-sensor-01"
}
~~~

JSON không cho phép comment dạng `//` hoặc dấu phẩy sau field cuối. Field `_comment` là text mà script
bỏ qua, dùng để nhắc file có secret.

## 6. Ý nghĩa và giới hạn từng field

| Field JSON | NVS key | Bắt buộc | Giới hạn | Ý nghĩa |
|---|---|---:|---:|---|
| `confirmProvisioning` | Không lưu | Có | Boolean | Khóa chống ghi nhầm; phải đổi thành `true` trước khi chạy thật. |
| `serialPort` | Không lưu | Có | COM hợp lệ | Port USB-UART, ví dụ `COM5`. |
| `baudRate` | Không lưu | Có | Số dương | Phải khớp firmware; hiện dùng `115200`. |
| `wifiSsid` | `wifi_ssid` | Có | 32 byte UTF-8 | SSID Wi-Fi 2.4 GHz. |
| `wifiPassword` | `wifi_pass` | Có | 64 byte UTF-8 | Mật khẩu Wi-Fi. |
| `mqttUri` | `mqtt_uri` | Có | 128 byte UTF-8 | Broker nhìn từ ESP32, ví dụ IP LAN server. |
| `mqttUsername` | `mqtt_user` | Có | 64 byte UTF-8 | User Mosquitto, không phải tài khoản web/GHCR. |
| `mqttPassword` | `mqtt_pass` | Có | 64 byte UTF-8 | Mật khẩu Mosquitto. |
| `farmId` | `farm_id` | Có | 48 byte UTF-8 | Hiện phải là `farm_main` để khớp topic server. |
| `pondId` | `pond_id` | Có | 48 byte UTF-8 | ID kỹ thuật của ao, ví dụ `pond_01`. |
| `nodeId` | `node_id` | Có | 48 byte UTF-8 | ID duy nhất của ESP32 và socket MQTT. |

Giới hạn được tính theo **byte UTF-8**, không chỉ số ký tự hiển thị. Một ký tự tiếng Việt có thể
dùng nhiều byte. Identity chỉ chấp nhận chữ ASCII, số, dấu chấm, gạch dưới và gạch ngang để tránh
topic MQTT khó đoán hoặc sai mapping.

`mqttUri` của ESP32 phải là địa chỉ LAN như `mqtt://192.168.100.66:1883`. Không dùng:

- `localhost` hoặc `127.0.0.1`, vì đó là chính ESP32;
- hostname Docker `mosquitto`, vì ESP32 không nằm trong Docker network;
- IP DHCP có thể thay đổi mà không có reservation.

## 7. Quy trình sử dụng

### Bước 1: chỉnh file

Mở:

~~~powershell
notepad .\provisioning.local.json
~~~

Thay toàn bộ chuỗi bắt đầu bằng `THAY_`. Xác nhận COM, IP broker, Node ID và Pond ID.

### Bước 2: validation, chưa ghi thiết bị

~~~powershell
.\provision-esp32.ps1 -ValidateOnly
~~~

Validation kiểm tra:

- JSON parse được;
- đủ field;
- byte length không vượt firmware;
- COM có dạng hợp lệ;
- baud là số dương;
- MQTT URI dùng scheme `mqtt` hoặc `mqtts`;
- identity có ký tự an toàn;
- còn placeholder hay không;
- cờ xác nhận đã bật hay chưa.

`-ValidateOnly` không mở COM và không thay đổi ESP32.

### Bước 3: mở khóa có chủ đích

Sau khi kiểm tra đúng board và file, đổi:

~~~json
"confirmProvisioning": true
~~~

Cờ này giảm nguy cơ chạy nhầm template hoặc ghi nhầm ESP32. Nó không phải một secret.

### Bước 4: đóng ứng dụng đang giữ COM

Thoát Serial Monitor bằng `Ctrl+C`. Đóng Arduino IDE/PuTTY nếu đang mở cùng port.

### Bước 5: provision tự động

~~~powershell
.\provision-esp32.ps1
~~~

Hoặc override COM mà không sửa file:

~~~powershell
.\provision-esp32.ps1 -Port COM7
~~~

Script thực hiện theo thứ tự:

1. mở COM với baud trong JSON;
2. chờ ESP32 khởi động nếu việc mở port làm board reset;
3. gửi tám lệnh `set`;
4. chờ NVS commit sau từng khóa;
5. gửi `status`;
6. gửi `restart`;
7. đóng COM trong `finally` kể cả khi có lỗi.

Script không in giá trị cấu hình. Nếu firmware/driver echo text, các giá trị đã nhập được thay bằng
`***` trước khi xuất ra PowerShell.

### Bước 6: kiểm tra sau restart

Đợi vài giây rồi có thể mở monitor:

~~~powershell
pio device monitor -p COM5 -b 115200
~~~

Kiểm tra Wi-Fi nhận IP, SNTP có UTC và MQTT connected. Việc provisioning script hoàn tất chỉ chứng
minh các lệnh đã được gửi/lưu; trạng thái kết nối thật vẫn phải được xác nhận bằng log và AquaOps.

## 8. Các tùy chọn của script

### `-ConfigPath`

Dùng một file local khác:

~~~powershell
.\provision-esp32.ps1 -ConfigPath .\provisioning.pond01.local.json
~~~

Pattern `provisioning*.local.json` đã bị Git ignore. Có thể giữ một file riêng cho từng node trên máy
kỹ thuật viên, nhưng cần bảo vệ máy và xóa file khi không còn cần.

### `-Port`

Override `serialPort` trong JSON:

~~~powershell
.\provision-esp32.ps1 -Port COM8
~~~

### `-ValidateOnly`

Chỉ kiểm tra file, không mở COM:

~~~powershell
.\provision-esp32.ps1 -ValidateOnly
~~~

### `-NoRestart`

Lưu NVS nhưng không restart:

~~~powershell
.\provision-esp32.ps1 -NoRestart
~~~

Firmware vẫn dùng cấu hình runtime cũ đến lần boot sau. Chỉ dùng tùy chọn này khi cần kiểm tra nhiều
thao tác trước khi restart.

## 9. Tính lặp lại và mất kết nối giữa chừng

Mỗi khóa được `nvs_commit` riêng. Đây không phải transaction bao phủ cả tám khóa. Nếu rút cáp hoặc
mất nguồn giữa chừng, NVS có thể chứa một phần cấu hình mới và một phần cũ.

Cách phục hồi:

1. cắm lại đúng ESP32;
2. kiểm tra lại file local;
3. chạy lại toàn bộ script;
4. không chỉ gửi khóa cuối bị nghi thiếu;
5. để script restart;
6. kiểm tra status/kết nối.

`nvs_set_str` ghi đè cùng key, nên chạy lại toàn bộ là idempotent về giá trị cuối. Tuy nhiên reboot
giữa chừng có thể tạm dùng cấu hình hỗn hợp; vì vậy không rút nguồn trong lúc script đang chạy.

## 10. Quy trình cho nhiều ESP32

Mỗi node phải có:

- một `nodeId` duy nhất;
- socket duy nhất trên AquaOps;
- Pond ID đúng;
- MQTT ACL phù hợp với topic prefix;
- hồ sơ thiết bị ghi lại board/MAC/vị trí;
- file local được bảo vệ hoặc xóa sau commissioning.

Ví dụ:

~~~text
provisioning.pond01-node01.local.json
provisioning.pond01-node02.local.json
~~~

Chạy:

~~~powershell
.\provision-esp32.ps1 -ConfigPath .\provisioning.pond01-node01.local.json -Port COM5
~~~

Không cắm hai board rồi dùng cùng Node ID; broker có thể ngắt client cũ do trùng MQTT Client ID và
server không còn xác định đúng nguồn dữ liệu.

## 11. Bảo mật

### 11.1 Kiểm tra file local không được Git theo dõi

~~~powershell
git check-ignore -v .\provisioning.local.json
git status --short
~~~

Lệnh đầu phải chỉ ra rule trong `.gitignore`. File local không được xuất hiện trong `git status`.

Trước mỗi commit:

~~~powershell
git diff --cached --stat
git diff --cached
~~~

### 11.2 Điều script không làm

- Không ghi secret vào command-line argument hoặc PowerShell history.
- Không ghi secret vào source/header.
- Không tạo binary riêng có credential.
- Không đưa secret vào output bình thường.
- Không thay broker ACL hoặc firewall.
- Không mã hóa file JSON trên đĩa.

File local hiện là plaintext. Chỉ lưu trên máy kỹ thuật viên được bảo vệ bằng tài khoản Windows,
BitLocker và quyền truy cập phù hợp. Khi hoàn tất commissioning, cân nhắc xóa file local và lưu secret
trong password manager có kiểm soát.

### 11.3 Rotation

Khi đổi mật khẩu Wi-Fi/MQTT:

1. cập nhật broker/AP theo kế hoạch tránh khóa toàn bộ node;
2. cập nhật file local;
3. provision một node thử;
4. xác nhận reconnect/telemetry/replay;
5. triển khai từng nhóm;
6. thu hồi credential cũ;
7. không thay tất cả node đồng thời khi chưa có rollback.

## 12. Xử lý lỗi

### Không tìm thấy file

~~~text
Không tìm thấy file cấu hình ...
~~~

Copy example thành local hoặc truyền đúng `-ConfigPath`.

### Còn placeholder hoặc `confirmProvisioning=false`

Đây là khóa an toàn. Điền giá trị thật, chạy `-ValidateOnly`, kiểm tra đúng board rồi mới đổi cờ.

### COM port không tồn tại

~~~powershell
pio device list
~~~

Kiểm tra cáp dữ liệu, driver và Device Manager.

### Access denied hoặc port đang bận

Đóng mọi Serial Monitor và chương trình khác dùng COM. Không chạy hai provisioning script đồng thời.

### Script bị chặn bởi execution policy

Kiểm tra chính sách tổ chức trước. Với file do chính repository tin cậy cung cấp, có thể mở
PowerShell từ VS Code hoặc hỏi quản trị viên cách cho phép script đã review. Không thay đổi execution
policy toàn máy một cách tùy tiện.

### Script hoàn tất nhưng ESP32 không online

Kiểm tra:

- Wi-Fi chỉ 2.4 GHz;
- MQTT IP/port đi được từ VLAN thiết bị;
- Mosquitto credential/ACL;
- `farm_main/pondId/nodeId` khớp socket;
- DNS/NTP; firmware cần UTC hợp lệ trước telemetry;
- log server có quarantine/Apply pending hay không.

### Chạy nhầm board

Không tiếp tục Apply trên server. Xác định board, provision lại identity đúng cho từng board và kiểm
tra broker không có hai client dùng cùng Node ID.

## 13. Khi nào vẫn cần Serial Monitor

File provisioning loại bỏ việc nhập tay nhưng Serial Monitor vẫn cần cho:

- xem log boot/panic;
- kiểm tra READY/INCOMPLETE;
- xác nhận Wi-Fi reason code;
- xác nhận SNTP/MQTT;
- xử lý lỗi NVS;
- chẩn đoán reconnect và offline replay.

Nói cách khác, monitor không còn là giao diện nhập cấu hình bắt buộc, nhưng vẫn là công cụ chẩn đoán
firmware.

## 14. Thuật ngữ

- **Local configuration**: cấu hình chỉ tồn tại trên máy kỹ thuật viên, không thuộc source control.
- **Placeholder**: giá trị mẫu buộc người dùng thay trước khi thực thi thật.
- **Guard flag**: cờ xác nhận ngăn thao tác ghi ngoài ý muốn.
- **UART provisioning**: nạp cấu hình bootstrap qua cổng serial.
- **NVS namespace**: vùng key-value logic trong flash; ở đây là `aqua_boot`.
- **Idempotent overwrite**: chạy lại cùng file tạo cùng giá trị cuối cho từng key.
- **Partial update**: chỉ một phần key đã commit khi thao tác bị gián đoạn.
- **Credential rotation**: đổi secret có kế hoạch và thu hồi secret cũ.
- **UTF-8 byte length**: dung lượng mã hóa, có thể lớn hơn số ký tự nhìn thấy.

## 15. Đọc tiếp

- [Runbook PlatformIO đầy đủ](PLATFORMIO-RUNBOOK.md)
- [Xử lý lỗi PlatformIO/ESP32](PLATFORMIO-TROUBLESHOOTING.md)
- [ESP32 bootstrap provisioning](../../Docs/13.ESP32-PROVISIONING.md)
- [Kiến trúc PlatformIO](../PLATFORMIO.md)
- [Microsoft SerialPort](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport)
- [PlatformIO device list](https://docs.platformio.org/en/latest/core/userguide/device/cmd_list.html)


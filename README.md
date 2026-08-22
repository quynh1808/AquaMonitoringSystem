# AquaOps — Aquaculture Monitoring

AquaOps là ứng dụng Blazor Interactive Server trên .NET 10 để giám sát một ao pilot, ingest telemetry MQTT, quản lý vụ nuôi, cảnh báo và điều khiển thiết bị Mitsubishi FX5U-compatible qua Modbus TCP có audit/read-back. Dữ liệu bền vững nằm trong PostgreSQL. Hệ thống chạy HTTP trong mạng LAN và không cần domain/chứng chỉ Internet.

## Bắt đầu đọc và tiếp nhận source

- [Bản đồ toàn bộ source code](Docs/01.SOURCE-CODE-GUIDE.md): giải thích kiến trúc, luồng khởi động, telemetry, điều khiển, commissioning, vai trò của từng folder/file và phần riêng về mọi đuôi file.
- [Từ điển kỹ thuật](Docs/02.TECHNICAL-GLOSSARY.md): định nghĩa thuật ngữ .NET, Blazor, PostgreSQL, MQTT, Modbus, PLC, ESP32, bảo mật, container và nghiệp vụ thủy sản trong đúng ngữ cảnh AquaOps.
- [Chỉ mục component và module](Docs/03.COMPONENT-INDEX.md): đi nhanh từ một module tới tài liệu cạnh source.
- [Thiết lập PostgreSQL trên Windows/Ubuntu](Docs/16.POSTGRESQL-SETUP-WINDOWS-UBUNTU.md): cài đặt, credential, migration, backup/restore và xử lý lỗi database.

Thứ tự khuyến nghị cho người mới là README → bản đồ source → từ điển khi gặp thuật ngữ → companion Markdown cạnh file đang đọc → runbook tương ứng trong **Docs/**. Tài liệu milestone và validation mô tả một thời điểm lịch sử; README và bản đồ source mô tả hiện trạng.

## Kiến trúc triển khai

```text
Trình duyệt LAN ─HTTP:8080─> AquaOps (.NET container từ GHCR)
                                  │
               ┌──────────────────┼──────────────────┐
               │                  │                  │
            PostgreSQL           Mosquitto       ESP32/FX5U Modbus TCP
         dữ liệu + audit   MQTT telemetry    command + read-back
```

Source không có Dockerfile. GitHub Actions dùng target `PublishContainer` của .NET SDK để build OCI image và đẩy lên GHCR. Ubuntu chỉ pull source vận hành (`compose.yaml`, script, PostgreSQL init) và pull image đã build; server không compile source.

## Tính năng hiện có

- Cookie authentication, PBKDF2, lockout, tự đổi/reset/recovery mật khẩu, thu hồi phiên khi khóa user/đổi mật khẩu/đổi quyền, audit đăng nhập–đăng xuất và phân quyền theo ao.
- Server-side authorization tại service; mọi dashboard/query nghiệp vụ đều lọc theo ao được cấp, Viewer chỉ đọc.
- Khu vực Administrator **Thiết lập hệ thống** gồm Thiết lập chung, Network & Connections, Edge nodes và nhật ký kết nối.
- MQTT broker profile và socket ESP32/Pi/STM32/PLC được cấu hình trên web, lưu PostgreSQL; secret MQTT mã hóa bằng Data Protection và không trả ngược ra UI/log.
- Dynamic MQTT/Modbus supervisor start/stop/reconnect theo dữ liệu PostgreSQL, exponential backoff, runtime health, last seen và log có thời hạn; một socket lỗi không dừng socket khác.
- Desired/reported configuration có `Draft → Applying → Applied/Rejected/Timeout/PendingReconnect/OutOfSync`; chỉ Applied khi node ACK đúng CommandId/version.
- Sensor pipeline có bounded backpressure, kiểm tra timestamp/giới hạn vật lý do edge báo cáo, EventId chống trùng, sequence gap và backfill không làm lùi live state.
- PostgreSQL/EF Core có migration versioned, foreign key cho quan hệ ao–asset–telemetry, unique partial index cho EventId/channel/vụ mở và transaction cho tồn kho, chu kỳ nuôi cùng audit.
- `latest_sensor_states` giữ đúng một trạng thái cho mỗi sensor; lịch sử nằm trong `sensor_readings` với index theo sensor/ao/metric/thời gian, BRIN và retention mặc định 365 ngày.
- Một ao pilot `pond_01` khởi tạo **không có sensor hoặc thiết bị mặc định**; Tổng quan, Dashboard và Analytics chỉ dựng card/metric từ asset mà admin thực sự thêm.
- Xóa sensor/thiết bị là decommission hai giai đoạn: ẩn và khóa ngay, loại khỏi desired config, chỉ archive sau node/PLC ACK; history, installation, command và audit không bị hard-delete.
- ESP32 báo capabilities cùng physical min/max/driver và sensor health; node/sensor lạ đi vào quarantine, không tự đăng ký.
- ESP32 có NVS ring buffer; chỉ xóa EventId sau ACK cấp ứng dụng xác nhận PostgreSQL đã persist, không dựa riêng vào MQTT QoS ACK.
- Sensor replacement giữ logical SensorId nhưng tạo SensorInstallationId mới; hỗ trợ MaintenanceOffline cho cả node và tránh alert storm.
- Ngưỡng cảnh báo vận hành cấu hình theo PondId + Metric; giới hạn vật lý nằm trong firmware/capabilities.
- Cảnh báo có new/acknowledged/resolved, số lần lặp, người tiếp nhận, delay/cooldown/hysteresis, webhook LAN, automation và audit.
- Thiết bị có AUTO, Remote Manual và Physical/Local Override với ưu tiên `Safety > Local > Remote > Auto`; PLC chặn lệnh thật sự khi Local Override.
- Thiết bị ở Draft/Applying/PendingReconnect không thể điều khiển; START/STOP và đổi mode chỉ mở sau trạng thái Applied.
- Remote Manual dùng hai pulse M riêng cho START/STOP, durable intent, read-back run/fault và không replay command cũ sau reconnect.
- AUTO policy chỉnh trên web có hysteresis, persistence, minimum ON/OFF, maximum starts/hour, lockout và input-failure action; PLC vẫn giữ interlock cuối.
- Hai ESP-IDF project cho WROOM-32E: sensor edge có interval động/health/durable sync và FX5U-compatible simulator FC01/03/05/06 qua Wi-Fi.
- Bootstrap firmware lưu Wi-Fi/broker/node identity trong NVS qua UART provisioning, không hardcode thông tin từng node trong source.
- Vụ nuôi có ràng buộc một vụ mở/ao, chuyển trạng thái hợp lệ, tăng trưởng, hao hụt, số con/khối lượng thu hoạch, biomass và FCR theo tăng biomass ròng; feeding gắn đúng CycleId.
- Nghiệp vụ P2 tại /farm-operations: kho theo lô/hạn dùng, sức khỏe và điều trị, an toàn sinh học, work order/SOP, chi phí theo ao/vụ và truy xuất lô thu hoạch.
- Dashboard không tạo dữ liệu giả; số liệu chưa có nguồn đo/nghiệp vụ hiển thị `no-data` hoặc 0 có chủ đích.
- Ba profile `Simulation`, `EdgeSimulation`, `Production` dùng chung MQTT/Modbus contract; startup guard kiểm tra profile Production.
- Readiness/liveness health check, persistent Data Protection keys và container filesystem read-only.
- AquaOps chạy non-root UID 1654; init container chuẩn hóa quyền volume key trước khi ứng dụng khởi động.
- CI build/test và publish image bất biến `sha-<commit>` hoặc tag `v*` lên GHCR.

## Chạy phát triển trên Windows

Cần .NET SDK phiên bản trong `global.json` và PostgreSQL cục bộ:

```powershell
dotnet tool restore
dotnet restore AquacultureMonitoring.sln --locked-mode
dotnet build AquacultureMonitoring.sln -c Release --no-restore -m:1
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build
dotnet run --project AquacultureMonitoring.csproj
```

Mặc định là `EdgeSimulation`. Khi chạy trực tiếp trên Windows, cần PostgreSQL; Mosquitto broker và node socket được tạo tại `/admin/settings/connections`, không còn đọc `Config/sensor-connections.json`.

`authentication.json` và `.env` chỉ tạo tài khoản bootstrap khi bảng `users` còn rỗng. Chúng không đổi password của user đã tồn tại. Người dùng đổi password tại `/account/change-password`; Administrator reset user tại `/admin/users`. Khi mất toàn bộ quyền admin, dùng script recovery trong `scripts/README.md`.

`appsettings.json` cố ý để trống PostgreSQL password. Thiết lập database theo `Docs/16.POSTGRESQL-SETUP-WINDOWS-UBUNTU.md` và truyền secret bằng environment.

## Phát hành production

Runbook đầy đủ từ Windows đến Ubuntu LAN: `Docs/20.BUILD-DEPLOY-WINDOWS-UBUNTU-LAN.md`.

Quy trình chuẩn:

1. Commit source ở repository root này và push `main` hoặc tag `v*`.
2. Workflow `.github/workflows/publish-container.yml` restore lock file, build, test và publish image bằng .NET SDK.
3. Trên Ubuntu 24.04 LTS, pull source vận hành rồi chạy `./scripts/deploy-ubuntu.sh <image-tag>`.
4. Script pull image, cập nhật Compose, kiểm tra `/health/ready` và quay lại image tag trước nếu readiness thất bại.

Hướng dẫn đầy đủ: `Docs/19.DEPLOYMENT-GHCR-UBUNTU.md`.

Quy trình review/stage source an toàn trước khi push được mô tả tại `Docs/18.GIT-PUBLISH.md`.

## Chuyển simulation, edge simulation và thiết bị thật

Telemetry và actuator là hai đường độc lập. Có thể đưa sensor thật vào trước nhưng vẫn giữ `HARDWARE_CONTROL_ENABLED=false`. Cả ba chế độ dùng cùng desired/reported protocol; endpoint, SensorId/channel và M/D mapping được chỉnh trên web.

Một adapter thật triển khai `IDeviceCommandTransport`, đăng ký DI bằng `Name` duy nhất, và chỉ trả success sau ACK hoặc read-back. Interlock, emergency stop, chống chạy khô/quá tải phải nằm tại PLC/relay cục bộ, không đặt duy nhất trong web app.

Hướng dẫn chuyển chế độ: `Docs/11.OPERATING-MODES.md`. Commissioning động: `Docs/09.DYNAMIC-COMMISSIONING.md`.
Provision firmware: `Docs/13.ESP32-PROVISIONING.md`. PlatformIO: `Firmware/PLATFORMIO.md` và
`Firmware/esp32_sensor_node/PLATFORMIO-RUNBOOK.md`. Provision tự động không nhập từng lệnh:
`Firmware/esp32_sensor_node/PROVISIONING-FILE.md`. Cấu hình cũ chỉ để tham khảo:
`Docs/14.LEGACY-NODE-CONNECTION-REFERENCE.md`.

## Cấu hình và secrets

- `Config/*.json`: chỉ còn default ứng dụng; cấu hình node/broker/PLC không nằm trong source.
- `.env`: cấu hình riêng từng Ubuntu host, đã bị Git ignore.
- `secrets/mosquitto_passwords`: sinh bởi bootstrap script, không commit.
- `data_protection_keys`: Docker volume để cookie không mất hiệu lực khi đổi container.
- `packages.lock.json`: khóa dependency cho build lặp lại được.

`AQUAOPS_BIND_ADDRESS` nên là IPv4 tĩnh của Ubuntu trên LAN; dùng `0.0.0.0` chỉ khi máy không có interface ngoài LAN. Truy cập ứng dụng bằng `http://<IP-LAN>:<AQUAOPS_HTTP_PORT>`. Không port-forward cổng này trên router.

Các mật khẩu từng xuất hiện trong Git history phải được xem là đã lộ và cần rotate, kể cả khi file hiện tại đã xóa.

## Health và vận hành

- `/health/live`: process còn phục vụ HTTP.
- `/health/ready`: ứng dụng và PostgreSQL sẵn sàng.
- `/health`: alias readiness để tương thích.
- Cài đặt và backup/restore PostgreSQL: xem `Docs/16.POSTGRESQL-SETUP-WINDOWS-UBUNTU.md` và kiểm thử restore định kỳ.
- Security checklist: `Docs/15.SECURITY-HARDENING.md`.
- Chỉ mục module: `Docs/03.COMPONENT-INDEX.md`.
- Phạm vi và tiêu chí nghiệm thu P0–P2: `Docs/07.P0-P2-IMPLEMENTATION.md`.

## Các bước còn lại trước khi điều khiển production

- Cấu hình cùng register map trong GX Works3 và integration test socket/mapping với FX5U-32MT/D thật trên test bench.
- Cấu hình Mosquitto ACL + TLS/mTLS nếu MQTT đi qua mạng không tin cậy.
- Thêm monitoring tập trung (metrics/log/error tracking) và cảnh báo ngoài ứng dụng.
- Diễn tập backup/restore PostgreSQL, mất mạng, ACK timeout, fail-safe và rollback.
- Kiểm thử tải dài hạn cho Blazor circuits và lưu lượng sensor thực tế.
- Đánh giá an toàn điện/cơ khí bởi kỹ sư phụ trách trước khi bật master hardware switch.

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

AquaOps là ứng dụng Blazor Interactive Server trên .NET 10 để giám sát một ao pilot, ingest telemetry MQTT, quản lý vụ nuôi, cảnh báo và điều khiển thiết bị Mitsubishi FX5U-compatible qua Modbus TCP có audit/read-back. Dữ liệu bền vững nằm trong PostgreSQL. Hệ thống chạy HTTP trong mạng LAN và không cần domain/chứng chỉ Internet.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **AquaOps — Aquaculture Monitoring**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Các file cấp repository định nghĩa cách toàn bộ solution được xây dựng, cấu hình, khởi động hoặc được tiếp cận. Sai khác tại đây có phạm vi ảnh hưởng toàn hệ thống.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Đóng vai trò điểm vào cho developer, công cụ build và application host.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Đối chiếu nội dung tài liệu với Program.cs, project/solution, appsettings, Compose và workflow. Giá trị production phải đến từ environment hoặc database động, không hard-code.

- **appsettings.json** (27 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.
- **global.json** (7 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.
- **packages.lock.json** (226 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.
- **Program.cs** (253 dòng): các điểm vào nhận diện được gồm không có public type/route được trích xuất tự động.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Program.cs, AquacultureMonitoring.csproj, appsettings.json, compose*.yaml, .github/workflows và các module con.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Repository**: Toàn bộ cây source được Git quản lý. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Composition root**: Nơi ghép dependency thành ứng dụng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Application host**: Tiến trình .NET chứa web và background service. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Immutable image**: Container image gắn đúng một bản build. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Build solution ở Release, chạy test, validate Compose và kiểm tra không có secret hay output runtime trong diff.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](Docs/02.TECHNICAL-GLOSSARY.md)

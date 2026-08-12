# AquaOps — Aquaculture Monitoring

AquaOps là ứng dụng Blazor Interactive Server trên .NET 10 để giám sát một ao pilot, ingest telemetry MQTT, quản lý vụ nuôi, cảnh báo và điều khiển thiết bị Mitsubishi FX5U-compatible qua Modbus TCP có audit/read-back. Dữ liệu bền vững nằm trong MongoDB. Hệ thống chạy HTTP trong mạng LAN và không cần domain/chứng chỉ Internet.

## Kiến trúc triển khai

```text
Trình duyệt LAN ─HTTP:8080─> AquaOps (.NET container từ GHCR)
                                  │
               ┌──────────────────┼──────────────────┐
               │                  │                  │
            MongoDB           Mosquitto       ESP32/FX5U Modbus TCP
         dữ liệu + audit   MQTT telemetry    command + read-back
```

Source không có Dockerfile. GitHub Actions dùng target `PublishContainer` của .NET SDK để build OCI image và đẩy lên GHCR. Ubuntu chỉ pull source vận hành (`compose.yaml`, script, Mongo init) và pull image đã build; server không compile source.

## Trạng thái chức năng

- Cookie authentication, PBKDF2, lockout, tự đổi/reset/recovery mật khẩu, thu hồi phiên khi khóa user/đổi mật khẩu/đổi quyền, audit đăng nhập–đăng xuất và phân quyền theo ao.
- Server-side authorization tại service; mọi dashboard/query nghiệp vụ đều lọc theo ao được cấp, Viewer chỉ đọc.
- Sensor pipeline có bounded backpressure, kiểm tra timestamp/miền giá trị, EventId chống trùng, sequence gap, backfill không làm lùi live state và MongoDB history/latest state.
- Một ao pilot `pond_01`; sáu metric MQTT có topic/payload validation, QoS 1 phía ESP32 và offline timeout 120 giây.
- Cảnh báo có new/acknowledged/resolved, số lần lặp, người tiếp nhận, delay/cooldown/hysteresis, webhook LAN, automation và audit.
- Device command FX5U đi qua safety switch, durable intent, timeout và read-back `M101/M201/M301`; fault tại `M102/M202/M302`.
- PLC monitor đọc heartbeat `D100` và trạng thái mỗi 5 giây, đánh dấu offline khi endpoint mất kết nối hoặc scan bị treo.
- Hai ESP-IDF project cho WROOM-32E: sensor edge publish mỗi 30 giây và FX5U-compatible PLC simulator qua Wi-Fi.
- Vụ nuôi có ràng buộc một vụ mở/ao, chuyển trạng thái hợp lệ, tăng trưởng, hao hụt, số con/khối lượng thu hoạch, biomass và FCR theo tăng biomass ròng; feeding gắn đúng CycleId.
- Nghiệp vụ P2 tại /farm-operations: kho theo lô/hạn dùng, sức khỏe và điều trị, an toàn sinh học, work order/SOP, chi phí theo ao/vụ và truy xuất lô thu hoạch.
- Dashboard không tạo dữ liệu giả; số liệu chưa có nguồn đo/nghiệp vụ hiển thị `no-data` hoặc 0 có chủ đích.
- Ba profile `Simulation`, `EdgeSimulation`, `Production` dùng chung MQTT/Modbus contract; startup guard kiểm tra profile Production.
- Readiness/liveness health check, persistent Data Protection keys và container filesystem read-only.
- AquaOps chạy non-root UID 1654; init container chuẩn hóa quyền volume key trước khi ứng dụng khởi động.
- CI build/test và publish image bất biến `sha-<commit>` hoặc tag `v*` lên GHCR.

## Chạy phát triển trên Windows

Cần .NET SDK phiên bản trong `global.json` và MongoDB cục bộ:

```powershell
dotnet restore AquacultureMonitoring.sln --locked-mode
dotnet build AquacultureMonitoring.sln -c Release --no-restore
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build
dotnet run --project AquacultureMonitoring.csproj
```

Mặc định là `EdgeSimulation`. Khi chạy trực tiếp trên Windows, cần Mosquitto cục bộ và ghi đè `SensorConnections__Mqtt__Host=127.0.0.1`; mật khẩu production bắt buộc lấy từ environment.

`authentication.json` và `.env` chỉ tạo tài khoản bootstrap khi collection `users` còn rỗng. Chúng không đổi password của user đã tồn tại. Người dùng đổi password tại `/account/change-password`; Administrator reset user tại `/admin/users`. Khi mất toàn bộ quyền admin, dùng script recovery trong `scripts/README.md`.

## Phát hành production

Runbook đầy đủ từ Windows đến Ubuntu LAN: `Docs/BUILD-DEPLOY-WINDOWS-UBUNTU-LAN.md`.

Quy trình chuẩn:

1. Commit source ở repository root này và push `main` hoặc tag `v*`.
2. Workflow `.github/workflows/publish-container.yml` restore lock file, build, test và publish image bằng .NET SDK.
3. Trên Ubuntu 24.04 LTS, pull source vận hành rồi chạy `./scripts/deploy-ubuntu.sh <image-tag>`.
4. Script pull image, cập nhật Compose, kiểm tra `/health/ready` và quay lại image tag trước nếu readiness thất bại.

Hướng dẫn đầy đủ: `Docs/DEPLOYMENT-GHCR-UBUNTU.md`.

Quy trình review/stage source an toàn trước khi push được mô tả tại `Docs/GIT-PUBLISH.md`.

## Chuyển simulation, edge simulation và thiết bị thật

Telemetry và actuator là hai đường độc lập. Có thể đưa sensor thật vào trước nhưng vẫn giữ `HARDWARE_CONTROL_ENABLED=false`. Cả ba chế độ dùng chung SensorId/topic và M-device, vì vậy chuyển endpoint không đổi UI/service nghiệp vụ.

Một adapter thật triển khai `IDeviceCommandTransport`, đăng ký DI bằng `Name` duy nhất, và chỉ trả success sau ACK hoặc read-back. Interlock, emergency stop, chống chạy khô/quá tải phải nằm tại PLC/relay cục bộ, không đặt duy nhất trong web app.

Hướng dẫn chuyển chế độ: `Docs/OPERATING-MODES.md`. Chi tiết P03: `Docs/P03-IMPLEMENTATION.md`.

## Cấu hình và secrets

- `Config/*.json`: default có thể version-control; không lưu production secret.
- `.env`: cấu hình riêng từng Ubuntu host, đã bị Git ignore.
- `secrets/mosquitto_passwords`: sinh bởi bootstrap script, không commit.
- `data_protection_keys`: Docker volume để cookie không mất hiệu lực khi đổi container.
- `packages.lock.json`: khóa dependency cho build lặp lại được.

`AQUAOPS_BIND_ADDRESS` nên là IPv4 tĩnh của Ubuntu trên LAN; dùng `0.0.0.0` chỉ khi máy không có interface ngoài LAN. Truy cập ứng dụng bằng `http://<IP-LAN>:<AQUAOPS_HTTP_PORT>`. Không port-forward cổng này trên router.

Các mật khẩu từng xuất hiện trong Git history phải được xem là đã lộ và cần rotate, kể cả khi file hiện tại đã xóa.

## Health và vận hành

- `/health/live`: process còn phục vụ HTTP.
- `/health/ready`: ứng dụng và MongoDB sẵn sàng.
- `/health`: alias readiness để tương thích.
- Backup/restore: xem `scripts/MongoBackup.md` và kiểm thử restore định kỳ.
- Security checklist: `Docs/SECURITY-HARDENING.md`.
- Chỉ mục module: `Docs/COMPONENT-INDEX.md`.
- Phạm vi và tiêu chí nghiệm thu P0–P2: `Docs/P0-P2-IMPLEMENTATION.md`.

## Các bước còn lại trước khi điều khiển production

- Cấu hình cùng register map trong GX Works3 và integration test adapter hiện có với FX5U-32MT/D thật trên test bench.
- Cấu hình Mosquitto ACL + TLS/mTLS nếu MQTT đi qua mạng không tin cậy.
- Thêm monitoring tập trung (metrics/log/error tracking) và cảnh báo ngoài ứng dụng.
- Diễn tập backup/restore MongoDB, mất mạng, ACK timeout, fail-safe và rollback.
- Kiểm thử tải dài hạn cho Blazor circuits và lưu lượng sensor thực tế.
- Đánh giá an toàn điện/cơ khí bởi kỹ sư phụ trách trước khi bật master hardware switch.

# AquaOps — Aquaculture Monitoring

AquaOps là ứng dụng Blazor Interactive Server trên .NET 10 để giám sát ao nuôi, ingest dữ liệu simulation/MQTT/Modbus, quản lý vụ nuôi, cảnh báo và điều khiển thiết bị có audit. Dữ liệu bền vững nằm trong MongoDB; Mosquitto nhận telemetry từ gateway. Hệ thống được thiết kế chạy HTTP trong mạng LAN tin cậy và không cần domain/chứng chỉ Internet.

## Kiến trúc triển khai

```text
Trình duyệt LAN ─HTTP:8080─> AquaOps (.NET container từ GHCR)
                                  │
               ┌──────────────────┼──────────────────┐
               │                  │                  │
            MongoDB           Mosquitto          PLC/relay
         dữ liệu + audit   sensor telemetry   transport adapter
```

Source không có Dockerfile. GitHub Actions dùng target `PublishContainer` của .NET SDK để build OCI image và đẩy lên GHCR. Ubuntu chỉ pull source vận hành (`compose.yaml`, script, Mongo init) và pull image đã build; server không compile source.

## Trạng thái chức năng

- Cookie authentication, PBKDF2, lockout, tự đổi/reset/recovery mật khẩu, role Administrator/FarmManager/Technician/Viewer và phạm vi theo ao.
- Server-side authorization tại service; Viewer chỉ đọc.
- Sensor pipeline có bounded backpressure, validation, MongoDB history/latest state và theo dõi adapter.
- Simulation, MQTT, Modbus TCP và Modbus RTU cho telemetry.
- Cảnh báo có delay/cooldown/hysteresis, automation và audit.
- Device command đi qua `IDeviceCommandTransport`, safety switch, durable intent, timeout và ACK/read-back trước khi cập nhật trạng thái.
- Vụ nuôi, tăng trưởng, hao hụt, thu hoạch, FCR, feeding, calibration và maintenance.
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

Mặc định chỉ bật simulation. Mật khẩu trong `Config/authentication.json` chỉ dành cho development; production bắt buộc ghi đè bằng `.env`/environment variable.

`authentication.json` và `.env` chỉ tạo tài khoản bootstrap khi collection `users` còn rỗng. Chúng không đổi password của user đã tồn tại. Người dùng đổi password tại `/account/change-password`; Administrator reset user tại `/admin/users`. Khi mất toàn bộ quyền admin, dùng script recovery trong `scripts/README.md`.

## Phát hành production

Quy trình chuẩn:

1. Commit source ở repository root này và push `main` hoặc tag `v*`.
2. Workflow `.github/workflows/publish-container.yml` restore lock file, build, test và publish image bằng .NET SDK.
3. Trên Ubuntu 24.04 LTS, pull source vận hành rồi chạy `./scripts/deploy-ubuntu.sh <image-tag>`.
4. Script pull image, cập nhật Compose, kiểm tra `/health/ready` và quay lại image tag trước nếu readiness thất bại.

Hướng dẫn đầy đủ: `Docs/DEPLOYMENT-GHCR-UBUNTU.md`.

Quy trình review/stage source an toàn trước khi push được mô tả tại `Docs/GIT-PUBLISH.md`.

## Thay simulation bằng thiết bị thật

Telemetry và actuator là hai đường độc lập. Có thể đưa sensor thật vào trước nhưng vẫn giữ `HARDWARE_CONTROL_ENABLED=false`. Mỗi thiết bị có `ControlTransport`, `ExternalAddress` và `FailSafeState`, nên chuyển từng thiết bị mà không đổi UI/service nghiệp vụ.

Một adapter thật triển khai `IDeviceCommandTransport`, đăng ký DI bằng `Name` duy nhất, và chỉ trả success sau ACK hoặc read-back. Interlock, emergency stop, chống chạy khô/quá tải phải nằm tại PLC/relay cục bộ, không đặt duy nhất trong web app.

Chi tiết và checklist commissioning: `Docs/HARDWARE-INTEGRATION.md`.

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

## Các bước còn lại trước khi điều khiển production

- Viết adapter PLC/relay đúng model thực tế và integration test bằng PLC simulator/test bench.
- Cấu hình Mosquitto ACL + TLS/mTLS nếu MQTT đi qua mạng không tin cậy.
- Thêm monitoring tập trung (metrics/log/error tracking) và cảnh báo ngoài ứng dụng.
- Diễn tập backup/restore MongoDB, mất mạng, ACK timeout, fail-safe và rollback.
- Kiểm thử tải dài hạn cho Blazor circuits và lưu lượng sensor thực tế.
- Đánh giá an toàn điện/cơ khí bởi kỹ sư phụ trách trước khi bật master hardware switch.

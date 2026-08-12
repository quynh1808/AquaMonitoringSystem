# Build và kiểm tra

## Build lặp lại được

```bash
dotnet restore AquacultureMonitoring.sln --locked-mode
dotnet build AquacultureMonitoring.sln -c Release --no-restore
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build
```

`global.json` cố định feature band .NET SDK; hai `packages.lock.json` cố định dependency graph. Khi chủ động nâng package, chạy `dotnet restore --use-lock-file`, review diff rồi commit lock file.

Project khai báo `RuntimeIdentifiers=linux-x64` để restore mặc định và restore publish container dùng cùng một dependency lock graph. Không xóa target `net10.0/linux-x64` khỏi `packages.lock.json`.

## Tạo container không cần Dockerfile

Để push thủ công (CI là lựa chọn chuẩn):

```bash
docker login ghcr.io
dotnet publish AquacultureMonitoring.csproj -c Release --os linux --arch x64 /t:PublishContainer \
  -p:ContainerRegistry=ghcr.io \
  -p:ContainerRepository=OWNER/aquaops \
  -p:ContainerImageTag=sha-COMMIT
```

Workflow `.github/workflows/publish-container.yml` tự thực hiện build/test/publish trên Ubuntu 24.04.

## Validate production Compose

```bash
cp .env.example .env
# Đổi mọi giá trị mẫu.
./scripts/bootstrap-ubuntu.sh
docker compose config --quiet
docker compose up -d
curl -fsS http://127.0.0.1:8080/health/ready
```

## Kiểm tra sensor và PLC

- Simulation: xem `/telemetry`, Last seen và số message tăng; kiểm tra `sensor_readings`/`telemetry_latest`.
- MQTT: bật adapter, publish payload trong `Examples/`, xác nhận cùng `SensorId` chuyển source và simulation nhường dữ liệu thật.
- Modbus: xác nhận UnitId, address base, register count, byte order, scale/offset bằng thiết bị chuẩn.
- Actuator: dùng PLC simulator/test bench; kiểm tra pending intent, ACK/read-back, timeout, mất mạng và không retry mù.

Xem thêm `Docs/HARDWARE-INTEGRATION.md` và `Docs/DEPLOYMENT-GHCR-UBUNTU.md`.

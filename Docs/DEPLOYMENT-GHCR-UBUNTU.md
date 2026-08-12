# Deployment HTTP LAN trên Ubuntu Server 24.04 LTS

## Mô hình được chọn

Windows là máy phát triển và push source. GitHub Actions dùng `.NET SDK PublishContainer` để tạo OCI image và push GHCR. Ubuntu chỉ pull source vận hành và image bất biến; server không cài .NET SDK và không compile source.

AquaOps publish HTTP trực tiếp trên IPv4/cổng LAN cấu hình trong `.env`. Không dùng Caddy, domain, ACME hay HTTPS. Mô hình này chỉ phù hợp với mạng nội bộ tin cậy; không port-forward cổng ứng dụng ra Internet.

```text
Máy trạm LAN ──HTTP──> Ubuntu:AQUAOPS_HTTP_PORT ──> AquaOps
                                                        ├──> MongoDB (Docker internal)
                                                        └──> Mosquitto (Docker internal/LAN tùy chọn)
```

## 1. Chuẩn bị repository trên Windows

Tại root có `AquacultureMonitoring.sln`:

```powershell
dotnet restore AquacultureMonitoring.sln --locked-mode
dotnet build AquacultureMonitoring.sln -c Release --no-restore
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build
git add .
git status
git commit -m "Update AquaOps"
git push origin main
```

Push `main` tạo image tag `sha-<full-commit-sha>`. Push Git tag `v*` tạo image cùng release tag. Chỉ deploy khi cả job `verify` và `publish` trên GitHub Actions thành công.

## 2. Chuẩn bị Ubuntu 24.04 LTS

1. Cài Docker Engine và Compose plugin từ official Docker apt repository.
2. Gán IPv4 tĩnh hoặc DHCP reservation cho Ubuntu trên LAN.
3. Clone source vào `/opt/aquaops`; không dùng root cho thao tác Git thường ngày.
4. Không cấu hình port-forward/NAT từ Internet đến cổng AquaOps hoặc MQTT.

```bash
sudo mkdir -p /opt/aquaops
sudo chown "$USER":"$USER" /opt/aquaops
git clone https://github.com/quynh1808/AquaMonitoringSystem.git /opt/aquaops
cd /opt/aquaops
cp .env.example .env
chmod +x scripts/*.sh
nano .env
```

Cấu hình quan trọng:

```env
AQUAOPS_IMAGE=ghcr.io/quynh1808/aquaops
AQUAOPS_VERSION=sha-<full-commit-sha>
AQUAOPS_BIND_ADDRESS=192.168.1.20
AQUAOPS_HTTP_PORT=8080
```

`AQUAOPS_BIND_ADDRESS` nên là IPv4 tĩnh thật của Ubuntu. Có thể dùng `0.0.0.0` để bind mọi interface nhưng chỉ nên dùng khi server không có interface ngoài LAN. Đổi mọi password mẫu; quote giá trị chứa khoảng trắng hoặc ký tự shell bằng dấu nháy đơn.

Nếu GHCR private, tạo PAT classic chỉ có `read:packages` rồi đăng nhập mà không ghi token vào history:

```bash
read -rsp "GHCR PAT: " CR_PAT
echo
printf '%s' "$CR_PAT" | docker login ghcr.io -u quynh1808 --password-stdin
unset CR_PAT
```

## 3. Giới hạn truy cập LAN

Không dựa duy nhất vào NAT của router. Chỉ cho subnet quản trị/nuôi trồng truy cập cổng ứng dụng. Docker-published ports có thể tương tác với UFW khác dự kiến, vì vậy xác nhận thực tế từ một máy ngoài subnet hoặc áp dụng rule tại `DOCKER-USER`/firewall biên phù hợp với hệ thống mạng.

Ví dụ UFW cơ bản cho LAN `192.168.1.0/24`:

```bash
sudo ufw allow from 192.168.1.0/24 to any port 8080 proto tcp
sudo ufw status
```

MQTT mặc định bind `127.0.0.1`. Chỉ đổi `MQTT_BIND_ADDRESS` sang IP LAN khi gateway thật cần kết nối và đã giới hạn nguồn truy cập. MongoDB không publish port ra host.

HTTP truyền cookie và mật khẩu không mã hóa. Chỉ dùng trên LAN tin cậy; nếu LAN có khách, Wi-Fi dùng chung hoặc thiết bị không quản lý, hãy tách VLAN hoặc bổ sung TLS/VPN nội bộ.

## 4. Bootstrap và deploy lần đầu

```bash
cd /opt/aquaops
./scripts/bootstrap-ubuntu.sh
./scripts/deploy-ubuntu.sh sha-<full-commit-sha>
```

Không nhập dấu `< >`; ví dụ tự lấy SHA hiện tại:

```bash
NEW_SHA="$(git rev-parse HEAD)"
./scripts/deploy-ubuntu.sh "sha-${NEW_SHA}"
```

Bootstrap tạo `secrets/mosquitto_passwords`, kiểm username, đặt owner/mode cho user Mosquitto và validate Compose. Khi stack khởi động, `data-protection-init` đặt named volume key về UID/GID non-root 1654 rồi thoát với mã `0`; đây là trạng thái bình thường. Deploy pull image, cập nhật Compose và kiểm tra `http://<bind-address>:<port>/health/ready`.

Kiểm tra:

```bash
docker compose ps
docker compose logs --tail=200 data-protection-init aquaops mongodb mosquitto
curl -fsS http://192.168.1.20:8080/health/live
curl -fsS http://192.168.1.20:8080/health/ready
```

Từ máy Windows trong cùng LAN, mở `http://192.168.1.20:8080`.

## 5. Cập nhật và rollback

Sau khi GitHub Actions publish commit mới thành công:

```bash
cd /opt/aquaops
./scripts/deploy-ubuntu.sh sha-<new-full-commit-sha>
```

Script dùng `git pull --ff-only`, đổi `AQUAOPS_VERSION`, pull image, recreate container và thử readiness HTTP. Nếu image mới không ready, script đưa app về image tag trước. Thay đổi schema/Compose không tự rollback; backup trước release có migration.

Rollback thủ công:

```bash
./scripts/deploy-ubuntu.sh sha-<known-good-full-commit-sha>
```

Không dùng `latest`. Giữ ít nhất một tag known-good và backup MongoDB gần nhất.

## 6. Recovery và dữ liệu bền vững

Nếu mất toàn bộ tài khoản Administrator:

```bash
./scripts/reset-admin-password.sh
```

Các named volume giữ MongoDB, Mosquitto và Data Protection keys khi container được thay. `docker compose down` không xóa volume; không dùng `docker compose down -v` trong vận hành.

Nếu log có `UnauthorizedAccessException` tại `/var/lib/aquaops/keys`, xem `Docs/TROUBLESHOOTING.md`. Không xóa volume key để sửa permission; init service sửa tại chỗ và giữ lại cookie/key hiện có.

Thiết lập timer cho `scripts/backup-mongodb.sh`, mã hóa backup, lưu một bản ngoài máy chủ và diễn tập restore định kỳ.

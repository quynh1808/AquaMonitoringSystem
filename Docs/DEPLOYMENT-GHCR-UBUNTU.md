# Deployment GHCR trên Ubuntu Server 24.04 LTS

## Mô hình được chọn

Windows là máy phát triển và push source. GitHub Actions là build authority, dùng `.NET SDK PublishContainer` để tạo OCI image và push GHCR. Ubuntu chỉ chạy Docker Compose bằng image bất biến; không cần SDK và không build source.

Mô hình này tách build khỏi production, giảm khác biệt Windows/Linux, cho phép rollback theo tag và tránh commit cả output `publish` vào Git.

## 1. Chuẩn bị repository trên Windows

Repository Git phải nằm tại root có `AquacultureMonitoring.sln`, không đặt Git repository bên trong `App_Data`.

```powershell
dotnet restore AquacultureMonitoring.sln --use-lock-file
dotnet build AquacultureMonitoring.sln -c Release --no-restore
dotnet test Tests/AquacultureMonitoring.Tests.csproj -c Release --no-build
git add .
git commit -m "Production container deployment"
git push origin main
```

Push `main` tạo tag image `sha-<full-commit-sha>`. Push Git tag như `v1.0.0` tạo image cùng tag đó. Nếu GHCR package để private, Ubuntu cần đăng nhập; public package có thể pull anonymous.

## 2. Chuẩn bị Ubuntu 24.04 LTS

1. Cài Docker Engine và Compose plugin từ official Docker apt repository.
2. Trỏ DNS A/AAAA của domain về server và mở TCP 80/443, UDP 443 nếu dùng HTTP/3.
3. Clone source vào thư mục dành riêng, ví dụ `/opt/aquaops`.
4. Không publish MongoDB ra host. MQTT mặc định chỉ bind `127.0.0.1`; chỉ mở IP LAN sau khi cấu hình ACL/TLS và firewall.

Khi gắn USB/RS-485 trực tiếp, cấu hình các dòng `COMPOSE_FILE`, `MODBUS_RTU_DEVICE` và `DIALOUT_GID` mẫu trong `.env`; xem checklist hardware trước khi bật.

```bash
cd /opt/aquaops
cp .env.example .env
chmod +x scripts/*.sh
nano .env
```

Đổi toàn bộ password mẫu, domain, email ACME và `AQUAOPS_VERSION`. Nếu giá trị chứa ký tự đặc biệt hoặc khoảng trắng, quote đúng cú pháp shell vì script bootstrap sẽ source `.env`.

Với GHCR private, tạo GitHub PAT classic chỉ có `read:packages`, sau đó:

```bash
echo "$CR_PAT" | docker login ghcr.io -u YOUR_GITHUB_USER --password-stdin
```

Không ghi PAT vào repository hay `.env` của ứng dụng.

## 3. Bootstrap lần đầu

```bash
./scripts/bootstrap-ubuntu.sh
./scripts/deploy-ubuntu.sh sha-<full-commit-sha>
```

Bootstrap sinh `secrets/mosquitto_passwords`, chmod 600 và validate Compose. Caddy tự xin/gia hạn chứng chỉ khi DNS và cổng mạng hợp lệ.

Nếu không còn tài khoản Administrator đăng nhập được, không xóa collection `users`. Chạy recovery tại console server:

```bash
chmod +x scripts/reset-admin-password.sh
./scripts/reset-admin-password.sh
```

Script yêu cầu MongoDB đang chạy, nhập password không echo và khởi tạo một recovery container ngắn hạn. Password không được ghi vào `.env`, source, command argument hay log ứng dụng. Người có quyền dùng Docker vốn có quyền quản trị cao trên server, vì vậy vẫn phải giới hạn SSH/Docker group.

Kiểm tra:

```bash
docker compose ps
docker compose logs --tail=200 aquaops caddy mongodb mosquitto
curl -fsS https://your-domain.example/health/live
curl -fsS https://your-domain.example/health/ready
```

## 4. Cập nhật và rollback

Sau khi CI publish xong:

```bash
cd /opt/aquaops
./scripts/deploy-ubuntu.sh sha-<new-full-commit-sha>
```

Script dùng `git pull --ff-only`, cập nhật tag local trong `.env`, pull image, `docker compose up -d --remove-orphans`, rồi thử readiness. Nếu image mới không ready, tag app được trả về phiên bản trước. Thay đổi Compose/database schema không tự rollback; phải review release note và backup trước release có migration.

Rollback thủ công:

```bash
./scripts/deploy-ubuntu.sh sha-<known-good-full-commit-sha>
```

Không dùng `latest` trong production. Giữ ít nhất một known-good image tag và backup MongoDB gần nhất.

## 5. Dữ liệu bền vững

Các named volume giữ MongoDB, Mosquitto, Caddy certificates và Data Protection keys khi container được thay. `docker compose down` không xóa volume; không dùng `down -v` trong production.

Thiết lập cron/systemd timer cho `scripts/backup-mongodb.sh`, mã hóa bản backup và copy ra storage khác máy. Một backup chưa từng restore thử không được coi là backup đã xác nhận.

## 6. Lưu ý firewall Docker

Docker-published ports có thể tương tác với UFW khác dự kiến. Chỉ Compose publish 80/443 và MQTT bind được chỉ định; MongoDB/app chỉ dùng network nội bộ. Với MQTT mở LAN, giới hạn nguồn bằng `DOCKER-USER`/firewall phù hợp, không phơi 1883 ra Internet.

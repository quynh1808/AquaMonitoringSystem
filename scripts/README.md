# Script vận hành AquaOps

## Mục đích và vai trò

Thư mục này biến các thao tác dễ sai khi làm thủ công thành quy trình có kiểm tra: bootstrap host,
deploy image bất biến, backup/restore PostgreSQL, recovery admin và xác minh static assets. Script
không thay thế việc hiểu Docker/Git/database; chúng chuẩn hóa đúng thứ tự và fail fast khi thiếu
biến môi trường.

## Danh sách script hiện tại

- `bootstrap-ubuntu.sh`: kiểm tra `.env`, credential PostgreSQL/HTTP LAN, tạo password file
  Mosquitto mode `600`, rồi validate Compose.
- `deploy-ubuntu.sh [image-tag]`: fast-forward source, pull image GHCR, recreate stack và kiểm tra
  `/health/ready`; nếu release mới không ready thì trả application image về tag trước.
- `backup-postgresql.sh [destination]`: tạo `pg_dump` custom-format trên Ubuntu Compose.
- `restore-postgresql.sh <backup.dump>`: yêu cầu gõ `RESTORE`, dừng app, khôi phục database và chạy
  lại app khi `pg_restore` thành công.
- `backup-postgresql.ps1`: backup PostgreSQL Compose trên Windows mà không đưa binary dump qua
  PowerShell text pipeline.
- `restore-postgresql.ps1 -BackupFile <path>`: restore có xác nhận trên Windows Compose.
- `reset-admin-password.sh`: recovery admin bằng container chạy một lần trên Ubuntu.
- `reset-admin-password.ps1`: recovery trực tiếp trên Windows; thêm `-Docker` khi dùng Compose.
- `verify-ui-assets.ps1`: xác nhận static assets và anonymous page contract.

PostgreSQL setup, role, migration và diễn tập restore được giải thích tại
`../Docs/16.POSTGRESQL-SETUP-WINDOWS-UBUNTU.md`.

## Chuẩn bị Ubuntu lần đầu

```bash
cp .env.example .env
chmod 600 .env
chmod +x scripts/*.sh postgres-init/*.sh
./scripts/bootstrap-ubuntu.sh
```

Không chạy deploy đồng thời từ nhiều SSH session. `.env`, `secrets/` và `backups/` không được
commit. Ứng dụng chỉ dùng HTTP LAN; PostgreSQL không publish port `5432` ra host.

## Deploy

```bash
./scripts/deploy-ubuntu.sh "sha-<commit-day-du>"
docker compose ps
docker compose logs --tail=200 aquaops postgresql
```

`deploy-ubuntu.sh` rollback application image, không rollback schema. Vì vậy migration phải tương
thích ngược với image trước trong cùng cửa sổ rollback; thay đổi phá schema cần quy trình expand →
migrate data → contract qua nhiều release.

## Backup và restore

Ubuntu:

```bash
./scripts/backup-postgresql.sh
./scripts/restore-postgresql.sh backups/postgresql/aquaops-YYYYMMDDTHHMMSSZ.dump
```

Windows Compose:

```powershell
.\scripts\backup-postgresql.ps1
.\scripts\restore-postgresql.ps1 -BackupFile .\backups\postgresql\aquaops-YYYYMMDDTHHMMSSZ.dump
```

Custom format của `pg_dump` hỗ trợ `pg_restore --clean --if-exists`. Restore là thao tác thay đổi dữ
liệu lớn nên script bắt xác nhận rõ ràng và dừng AquaOps để tránh ghi đồng thời.

Backup chưa được xem là hoàn chỉnh nếu chưa restore thử trên database/container tách biệt. Sau
restore phải kiểm tra migration history, đăng nhập, số asset, latest telemetry và tổng nghiệp vụ.

## Recovery mật khẩu Administrator

Ubuntu/Compose, khi service `postgresql` đang chạy:

```bash
./scripts/reset-admin-password.sh
```

Windows chạy ứng dụng trực tiếp:

```powershell
.\scripts\reset-admin-password.ps1
```

Windows Compose:

```powershell
.\scripts\reset-admin-password.ps1 -Docker
```

Password được nhập ẩn, kiểm tra tối thiểu 12 ký tự và chỉ tồn tại tạm trong environment. Recovery
chỉ áp dụng cho user đã mang role `Administrator`; thao tác mở khóa, kích hoạt, thay hash PBKDF2 và
tăng `SessionVersion` để vô hiệu cookie cũ.

## Failure mode quan trọng

- `permission denied /var/run/docker.sock`: đăng nhập lại sau khi được thêm group `docker`, hoặc
  dùng `newgrp docker`; xem `Docs/21.TROUBLESHOOTING.md`.
- `PostgreSQL service is not running`: chạy `docker compose up -d postgresql` và xem log.
- `.env: command not found`: giá trị có khoảng trắng/ký tự shell phải được quote đúng.
- `local changes would be overwritten`: không sửa script tracked trực tiếp trên server; stash hoặc
  commit thay đổi có chủ đích trước `git pull --ff-only`.
- readiness fail sau deploy: đọc log app/database; không xóa volume để thử lại khi chưa có backup.

## Thuật ngữ

- **Bootstrap**: chuẩn bị prerequisite và secret lần đầu trên host.
- **Deploy**: đưa một release image cụ thể vào vận hành.
- **Immutable image**: image không bị build lại dưới cùng một tag release.
- **Readiness**: app có thể nhận traffic và kết nối được dependency bắt buộc.
- **Backup**: bản sao dữ liệu được tạo để phục hồi.
- **Restore rehearsal**: khôi phục thử có đo và đối chiếu, chứng minh backup thực sự dùng được.
- **Idempotent script**: script chạy lại không phá trạng thái đã đúng.

## Quan hệ với hệ thống

- `.env.example` định nghĩa input.
- `compose.yaml` định nghĩa service/network/volume.
- `postgres-init/` chỉ chạy khi volume PostgreSQL trống.
- `Program.cs` định nghĩa health endpoint và recovery command.
- `Data/Migrations/` là schema versioned được app áp dụng trước bootstrap.
- `Docs/19.DEPLOYMENT-GHCR-UBUNTU.md` và
  `Docs/20.BUILD-DEPLOY-WINDOWS-UBUNTU-LAN.md` mô tả quy trình release đầy đủ.

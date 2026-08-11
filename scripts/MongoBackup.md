# MongoDB backup và restore

Các script chạy `mongodump`/`mongorestore` bên trong container MongoDB, nên tự dùng credential và database name của container từ `.env`.

## Linux

```bash
./Scripts/backup-mongodb.sh
./Scripts/restore-mongodb.sh ./backups/20260725-120000/aquaculture_monitoring.archive.gz
```

Biến tùy chọn của script backup:

- `BACKUP_ROOT`.
- `BACKUP_RETENTION_DAYS`.

Có thể đặt backup tự động bằng cron, ví dụ 02:00 mỗi ngày:

```cron
0 2 * * * cd /opt/aquaops && BACKUP_RETENTION_DAYS=30 ./Scripts/backup-mongodb.sh >> ./backups/backup.log 2>&1
```

## Windows PowerShell

```powershell
.\Scripts\backup-mongodb.ps1
.\Scripts\restore-mongodb.ps1 -Archive .\backups\20260725-120000\aquaculture_monitoring.archive.gz
```

## Lưu ý

- Kiểm tra restore định kỳ trên môi trường thử nghiệm.
- Không lưu backup trên cùng một ổ đĩa duy nhất với MongoDB.
- Bảo vệ file `.env` và archive vì dữ liệu vận hành có thể nhạy cảm.
- Restore dùng `--drop`; collection hiện có sẽ bị thay thế bởi dữ liệu trong archive.

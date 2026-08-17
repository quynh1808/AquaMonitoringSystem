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

<!-- aquaops-doc-context-v1 -->

## Bối cảnh đầy đủ của tài liệu

### Mục đích cụ thể

Các script chạy `mongodump`/`mongorestore` bên trong container MongoDB, nên tự dùng credential và database name của container từ `.env`.

Tài liệu này đồng thời giúp người đọc xác định ranh giới trách nhiệm của **MongoDB backup và restore**, thay vì chỉ biết tên class, page hoặc quy trình.

### Vì sao thành phần hoặc quy trình này cần tồn tại

Thao tác deploy/backup/restore/recovery lặp bằng tay dễ sai và khó audit.

Nếu bỏ qua ranh giới này, logic dễ bị đặt sai layer, kiểm tra bị lặp hoặc trạng thái hiển thị không còn phản ánh nguồn dữ liệu có thẩm quyền.

### Vai trò trong toàn hệ thống

Tự động hóa runbook Windows/Ubuntu theo cách có kiểm tra và kết quả rõ.

Vai trò này phải được hiểu theo cả đường đi thành công và đường lỗi: thiếu dữ liệu, dependency offline, input không hợp lệ, người dùng thiếu quyền, retry và shutdown.

### Cách triển khai trong source

Script đọc tham số/env, fail fast, validate target, gọi Docker/Git/Mongo tooling và in bước xác minh.

- Tài liệu này là runbook hoặc hồ sơ quyết định; source có thẩm quyền được liên kết trong nội dung và trong bản đồ toàn repository.

Danh sách symbol ở trên được trích từ source để định hướng, không thay thế việc đọc method và comment về invariant/failure mode.

### Quan hệ với các file và module khác

Docs/19.BUILD-DEPLOY-WINDOWS-UBUNTU-LAN.md, 18.DEPLOYMENT-GHCR-UBUNTU.md, compose files và .env.

Khi contract dữ liệu, tên trạng thái, ID, route hoặc cấu hình thay đổi, phải rà soát cả bên tạo dữ liệu và bên tiêu thụ; chỉ sửa một phía có thể vẫn build nhưng sai lúc runtime.

### Thuật ngữ chuyên môn dùng trong phạm vi này

- **Bootstrap**: Chuẩn bị prerequisite lần đầu. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Deploy**: Đưa release vào vận hành. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Backup**: Tạo bản sao có thể phục hồi. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Restore**: Khôi phục từ bản sao. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.
- **Idempotent script**: Chạy lặp không phá trạng thái đúng. Trong AquaOps, khái niệm này tồn tại để giữ đúng ranh giới và hành vi của layer đang mô tả.

Các thuật ngữ liên quan khác, sự khác nhau giữa các khái niệm dễ nhầm và lý do sử dụng trong dự án được giải thích tại [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md).

### Cách đọc và sử dụng tài liệu này

1. Đọc phần mục đích để biết đầu ra mong đợi.
2. Mở file source hoặc cấu hình được liệt kê ở phần triển khai.
3. Theo quan hệ upstream để biết dữ liệu đến từ đâu và downstream để biết thay đổi ảnh hưởng ai.
4. Xem failure mode, authorization, idempotency và trạng thái no-data/offline trước khi sửa happy path.
5. Cập nhật tài liệu trong cùng commit với source để tránh kiến thức bị lỗi thời.

### Kiểm tra bắt buộc khi thay đổi

Kiểm tra cú pháp và chạy trong môi trường cô lập; backup chỉ đạt yêu cầu sau khi restore thử thành công.

Ngoài kiểm tra chuyên biệt, luôn rà soát diff để chắc chắn không có secret, credential, dữ liệu runtime, output **bin/obj** hoặc thay đổi ngoài phạm vi.

### Đọc tiếp

- [Bản đồ toàn bộ source code](../Docs/01.SOURCE-CODE-GUIDE.md)
- [Từ điển kỹ thuật](../Docs/02.TECHNICAL-GLOSSARY.md)

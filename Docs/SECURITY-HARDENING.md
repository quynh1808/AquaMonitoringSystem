# Security hardening checklist

## Trước lần chạy production đầu tiên

- Rotate toàn bộ credential từng xuất hiện trong source/Git history: Mongo root/app, MQTT, bootstrap admin, GHCR token.
- Dùng password ngẫu nhiên dài; không dùng giá trị trong `.env.example` hay `Config/authentication.json`.
- Đặt GHCR package private hoặc public có chủ đích; token server chỉ cần `read:packages`.
- DNS đúng, HTTPS hợp lệ, chỉ mở 80/443; MQTT chỉ mở LAN/VPN khi cần.
- Giữ `HARDWARE_CONTROL_ENABLED=false` tới khi hoàn thành checklist phần cứng.

## Trong Compose

- AquaOps chạy root filesystem read-only, `no-new-privileges`, chỉ expose cổng nội bộ.
- MongoDB nằm trên network internal và không publish port.
- Data Protection keys dùng named volume, tránh logout toàn bộ user sau deploy.
- Log container có rotation để không lấp đĩa.
- `.env`, `secrets/`, keys và backup đã Git ignore; giới hạn quyền file trên Ubuntu.

Environment variables vẫn có thể được người có quyền Docker inspect. Khi hạ tầng trưởng thành, chuyển secret sang Docker secrets/external secret manager và giới hạn quyền truy cập Docker daemon.

## Ứng dụng

- Cookie HttpOnly, SameSite Strict, Secure khi đi qua HTTPS và sliding expiration.
- PBKDF2-HMAC-SHA256, lockout, role và pond claims.
- Đổi/reset mật khẩu tạo salt mới, ghi `PasswordChangedAtUtc`, xóa lockout; recovery cục bộ chỉ chấp nhận Administrator.
- UI chỉ hỗ trợ presentation; service guard là điểm cưỡng chế quyền cuối cùng.
- Device command có durable intent, audit, timeout, ACK/read-back và fail-closed khi chưa ghi được intent.
- `/health/live` không kiểm dependency; `/health/ready` kiểm MongoDB.

## Việc nên bổ sung

- Bắt đổi password bootstrap khi đăng nhập lần đầu và hỗ trợ MFA/SSO cho Administrator.
- Bổ sung security-stamp để thu hồi các cookie khác và audit tập trung cho thay đổi user/password.
- Mosquitto ACL theo topic/device, TLS/mTLS và certificate rotation.
- Centralized logging/metrics, alert disk/CPU/RAM, Mongo replication/backup monitoring.
- Dependency/container vulnerability scan và SBOM/signature/provenance cho release có yêu cầu cao.
- Kiểm thử xâm nhập và review threat model trước khi Internet-facing.

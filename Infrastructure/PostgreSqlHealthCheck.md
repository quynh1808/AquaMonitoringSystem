# `PostgreSqlHealthCheck`

Health check tạo DbContext ngắn hạn từ pool và gọi `Database.CanConnectAsync`.

- `/health/live` không gọi database: chỉ chứng minh process còn phục vụ HTTP.
- `/health/ready` gồm check `postgresql`: chỉ xanh khi app kết nối được system of record.
- Compose/deploy script dùng readiness để không đưa container chưa sẵn sàng vào vận hành.

**Liveness** trả lời “process có sống không”; **readiness** trả lời “có thể nhận workload không”.
Không gộp database vào liveness vì database tạm lỗi không nên khiến orchestrator restart app liên
tục và tạo thêm tải.

File liên quan: `Program.cs`, `compose.yaml`, `scripts/deploy-ubuntu.sh` và
`Docs/21.TROUBLESHOOTING.md`.

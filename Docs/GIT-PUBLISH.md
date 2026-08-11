# Đưa toàn bộ source lên Git

## Trạng thái sau migration

`App_Data` từng là repository lồng chứa publish artifact và đã được xóa. Root source hiện không có `.git`; đây là chủ ý để không tự nối source mới vào lịch sử binary cũ hoặc force-push nhầm remote.

## Cách khuyến nghị

Tạo một GitHub repository mới/rỗng cho source, sau đó tại Windows PowerShell ở root này:

```powershell
git init -b main
git add .
git status
git commit -m "Initial production-ready AquaOps source"
git remote add origin https://github.com/quynh1808/AquaMonitoringSystem.git
git push -u origin main
```

Review `git status` trước commit: không được có `.env`, `secrets`, `bin`, `obj`, `App_Data`, backup hoặc Data Protection keys.

Nếu muốn dùng lại `quynh1808/SystemMonitoring`, hãy archive/tag lịch sử publish cũ trên GitHub trước, rồi migrate trong một clone riêng và review diff. Không dùng force-push trực tiếp từ thư mục hiện tại vì remote cũ có lịch sử khác.

Sau push `main`, GitHub Actions sẽ chạy build/test và tạo GHCR image `ghcr.io/OWNER/aquaops:sha-<commit>`. Có thể cần đổi `AQUAOPS_IMAGE` trong `.env.example`/host nếu owner khác `quynh1808`.

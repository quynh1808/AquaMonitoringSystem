# Đưa toàn bộ source lên Git

## Trạng thái repository

`App_Data` từng là repository lồng chứa publish artifact và đã được xóa. Repository source hiện dùng branch `main` và remote `https://github.com/quynh1808/AquaMonitoringSystem.git`.

## Cách khuyến nghị

Tại Windows PowerShell ở root source:

```powershell
git add .
git status
git diff --cached --stat
git commit -m "Mô tả thay đổi"
git push origin main
```

Review `git status` trước commit: không được có `.env`, `secrets`, `bin`, `obj`, `App_Data`, backup hoặc Data Protection keys.

Sau push `main`, GitHub Actions sẽ chạy build/test và tạo GHCR image `ghcr.io/OWNER/aquaops:sha-<commit>`. Có thể cần đổi `AQUAOPS_IMAGE` trong `.env.example`/host nếu owner khác `quynh1808`.

# Xử lý sự cố vận hành

## Data Protection báo Permission denied

Triệu chứng thường xuất hiện khi mở trang có form đăng nhập/antiforgery:

```text
UnauthorizedAccessException: Access to the path '/var/lib/aquaops/keys/*.tmp' is denied
CryptographicException: An error occurred while trying to encrypt the provided data
```

Named volume Docker mới mặc định thuộc `root`, trong khi AquaOps chạy non-root UID/GID `1654`. Compose có service một lần `data-protection-init` để `chown` volume và đặt mode `700`; `aquaops` chỉ chạy sau khi init hoàn thành thành công.

Sau khi cập nhật source/image:

```bash
docker compose up -d --remove-orphans
docker compose ps -a
docker compose logs data-protection-init aquaops
```

`data-protection-init` ở trạng thái `Exited (0)` là bình thường. Không xóa volume `data_protection_keys`, vì thao tác đó làm vô hiệu cookie hiện tại và mất key dùng để giải mã dữ liệu được Data Protection bảo vệ.

Cảnh báo `No XML encryptor configured` không làm request thất bại; nó cho biết key chưa được mã hóa thêm ở trạng thái lưu trữ. Trong mô hình LAN hiện tại, quyền volume `700` và quyền Docker/host hạn chế là lớp bảo vệ tại chỗ. Nếu yêu cầu bảo mật tăng, cấu hình certificate/KMS để mã hóa key at rest.

## Kiểm tra HTTP LAN

```bash
docker compose ps -a
docker compose port aquaops 8080
curl -fsS http://127.0.0.1:8080/health/live
curl -fsS http://127.0.0.1:8080/health/ready
```

Chỉ dùng `127.0.0.1` khi `AQUAOPS_BIND_ADDRESS=0.0.0.0` hoặc port được bind loopback. Nếu bind IPv4 LAN cụ thể, curl đúng địa chỉ đó.

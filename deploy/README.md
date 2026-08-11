# Reverse proxy

`Caddyfile` nhận domain và ACME email từ `.env`, tự cấp TLS, thêm security headers và reverse proxy WebSocket/HTTP tới `aquaops:8080`. Không expose trực tiếp Kestrel ra host.

DNS và TCP 80/443 phải trỏ/mở đúng trước khi khởi động. UDP 443 chỉ phục vụ HTTP/3 và có thể đóng nếu hạ tầng không dùng.

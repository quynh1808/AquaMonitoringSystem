# AlertNotificationService

Gửi payload cảnh báo tới webhook nội bộ bằng `IHttpClientFactory`, áp timeout cấu hình và lưu từng lần gửi vào MongoDB. Recipient audit chỉ giữ phần URL path, không giữ query string có thể chứa secret. Service không ném lỗi delivery để tránh dừng sensor ingestion.


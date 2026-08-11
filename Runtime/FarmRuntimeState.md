# FarmRuntimeState

Đây là cache runtime dùng chung giữa Dashboard và pipeline cảm biến.

- MongoDB vẫn là nguồn dữ liệu bền vững.
- Runtime state giữ snapshot mới nhất để Blazor render nhanh và phát sự kiện `Changed`.
- Lịch sử gần nhất được giữ trong RAM theo `RuntimeReadingLimit`; lịch sử đầy đủ nằm trong MongoDB.
- Mọi cập nhật đều đi qua method có khóa đồng bộ, tránh component đọc collection trong lúc worker đang sửa.

Khi triển khai nhiều instance web server, runtime state nên được thay bằng MongoDB change stream/Redis pub-sub để đồng bộ giữa các instance.

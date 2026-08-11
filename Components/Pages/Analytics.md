# Phân tích tổng thể

Component dùng route động `/analytics/{Section}` để tạo sáu trang nghiệp vụ độc lập:

- `/analytics/ponds`: ao nuôi và vụ nuôi.
- `/analytics/growth`: Biomass và tăng trưởng.
- `/analytics/water-quality`: chất lượng nước.
- `/analytics/feed`: thức ăn.
- `/analytics/equipment`: thiết bị và năng lượng.
- `/analytics/risks`: cảnh báo và rủi ro.

## Lý do dùng một component route động

Các trang có chung header, tab, nguồn dữ liệu và cơ chế refresh. Việc dùng một component tránh sao chép logic nhưng người dùng vẫn có URL và menu riêng cho từng miền phân tích.

## Nguồn dữ liệu

Tất cả trang dùng `DashboardAnalyticsService`, bảo đảm số liệu trên Dashboard và trang phân tích nhất quán.

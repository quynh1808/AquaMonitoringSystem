# DashboardAnalyticsService

Tạo snapshot theo danh sách PondId được phép xem, chỉ từ MongoDB/runtime. Weather, energy history, feed plan hoặc growth chưa có nguồn đo sẽ không được tạo giả. Trạng thái thiết bị lấy từ PLC monitor/read-back; telemetry lấy từ MQTT pipeline.

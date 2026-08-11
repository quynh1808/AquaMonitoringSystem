# MongoWriteQueue

The queue serializes non-critical UI persistence work through a bounded channel. Producers use
`EnqueueAsync`, which waits when capacity is exhausted. This backpressure is intentional: the
previous `TryWrite` API could return `false` and silently lose a farm mutation when the queue was
full.

Các event UI hiện tại là đồng bộ. Để không khóa Blazor circuit khi ghi MongoDB, thao tác UI cập nhật runtime trước rồi đưa lệnh ghi vào channel nền.

Pipeline cảm biến vẫn ghi `sensor_readings` trực tiếp và `await` để đảm bảo mẫu đã được lưu trước khi xử lý tiếp. Queue chủ yếu dùng cho thao tác người dùng như thêm ao, bật thiết bị, ghi thức ăn và đóng cảnh báo.

Channel có giới hạn để tránh tăng RAM vô hạn khi MongoDB mất kết nối. Lỗi ghi được log và không làm sập circuit UI.

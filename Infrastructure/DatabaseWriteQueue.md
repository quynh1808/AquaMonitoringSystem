# `DatabaseWriteQueue`

## Mục đích

Queue chứa tối đa 5.000 delegate ghi không critical từ UI/runtime. Khi đầy, `EnqueueAsync` chờ để
tạo **backpressure**, không silently drop mutation. Một reader xử lý tuần tự và log lỗi.

Telemetry không đi qua queue: `SensorIngestionWorker` await transaction lưu reading/latest state
trước ACK ứng dụng. Queue chủ yếu giữ Blazor circuit không phải chờ các mutation UI không cần phản
hồi database tức thời.

## Giới hạn quan trọng

Queue nằm trong RAM, không phải durable outbox. Nếu process dừng trước khi delegate chạy, work item
có thể mất. Những thao tác safety/command intent và telemetry bắt buộc phải ghi trực tiếp, await và
transactional. Khi PostgreSQL offline, bounded capacity ngăn RAM tăng vô hạn.

## Thuật ngữ

- **Bounded channel**: hàng đợi có capacity cố định.
- **Backpressure**: làm producer chậm lại khi consumer không theo kịp.
- **Durable outbox**: bảng bền vững cho event cần retry; queue này không phải outbox.
- **Graceful cancellation**: dừng reader theo host cancellation token.

File liên quan: `FarmStateService`, `Program.cs` và `SensorIngestionWorker`.

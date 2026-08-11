# SimulatedSensorSource

Nguồn dữ liệu giả lập dùng khi chưa có phần cứng.

## Mô hình giá trị

Mỗi metric sử dụng:

- giá trị cơ sở;
- min/max mô phỏng;
- bước ngẫu nhiên tối đa;
- hệ số kéo về giá trị mục tiêu;
- dao động ngày/đêm;
- xác suất và biên độ bất thường;
- offset riêng cho từng ao.

Tất cả nằm trong `Config/simulation-profile.json`.

## Chuyển dần sang phần cứng

Service chỉ mô phỏng sensor đang có `SourceType` là `simulation` hoặc `manual-simulation`. Khi MQTT/Modbus gửi bản tin thật cho cùng `SensorId`, ingestion đổi `SourceType`, cập nhật `ExternalAddress` và simulation tự ngừng đối với riêng sensor đó. Các sensor khác vẫn tiếp tục giả lập. `AllowSimulationOverrideRealSensors=false` ngăn bản tin giả đang chờ trong queue chuyển sensor thật trở lại simulation.

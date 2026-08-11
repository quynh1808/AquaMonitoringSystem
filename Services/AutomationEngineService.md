# AutomationEngineService

Automation policies evaluate validated sensor readings and request the desired device state.
Commands are delegated to `DeviceCommandService`; the automation engine never changes a device
state optimistically. Failed, blocked and timed-out transport commands are written to the
automation log for operator review.

Engine hiện chạy transport `simulation` để có thể thử đầy đủ logic khi chưa có tủ điện.

Điểm bật, điểm tắt và loại thiết bị được khai báo trong `Config/system-behavior.json`. Hai điểm khác nhau tạo hysteresis điều khiển, tránh contactor bật/tắt liên tục.

Mỗi thao tác tạo `DeviceCommand` và `AutomationLog`. Khi có phần cứng, thay transport simulation bằng MQTT/PLC command adapter nhưng giữ nguyên policy và UI. Liên động an toàn cuối cùng vẫn phải nằm tại PLC.

# AutomationEngineService

Đánh giá policy có hysteresis rồi gửi lệnh qua `DeviceCommandService`. P03 dùng transport FX5U với safety switch/read-back; automation không được tự coi write ACK là trạng thái tải. Interlock cuối cùng vẫn nằm tại PLC.

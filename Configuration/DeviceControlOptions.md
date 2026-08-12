# DeviceControlOptions

Cấu hình safety switch, timeout và endpoint/map Mitsubishi FX5U. Tên `M100` được adapter chuyển thành coil `0x2064`; operator không nhập địa chỉ decimal Modbus. Mỗi thiết bị bắt buộc có command, feedback và fault riêng. Endpoint Docker được ghi đè qua `FX5U_HOST`, `FX5U_PORT`, `FX5U_UNIT_ID`.

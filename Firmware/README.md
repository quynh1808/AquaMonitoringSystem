# Firmware edge AquaOps

- `esp32_sensor_node`: Edge Simulation/production sensor node, nhận desired config theo MQTT,
  mỗi channel mặc định interval 0, gửi telemetry + sensor health và giữ offline data trong NVS.
- `esp32_fx5u_plc_simulator`: WROOM-32E mô phỏng phần Modbus TCP cần cho FX5U-32MT/D,
  gồm heartbeat, config ACK/version, ba mode, START/STOP pulse, feedback và fault.
- `components/aqua_provisioning`: bootstrap Wi-Fi/broker/node identity qua UART và NVS dùng chung.

Không commit `sdkconfig` có secret. Quy trình: `Docs/ESP32-PROVISIONING.md`; chuyển Edge Simulation
sang phần cứng thật: `Docs/OPERATING-MODES.md`.

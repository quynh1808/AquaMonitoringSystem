# DeviceControlOptions

`DeviceControlOptions` separates actuator safety from sensor ingestion. Real MQTT/Modbus
telemetry can be enabled while `HardwareControlEnabled` remains `false`.

- `DefaultTransport`: transport selected when a device has no explicit mapping.
- `HardwareControlEnabled`: master switch for every non-simulation transport.
- `SimulationEnabled`: enables the deterministic simulation acknowledgement transport.
- `CommandTimeoutSeconds`: acknowledgement timeout that hardware transports must honor.

Production rollout is documented in `Docs/HARDWARE-INTEGRATION.md`.

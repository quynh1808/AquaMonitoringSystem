# DeviceCommandService

`DeviceCommandService` is the actuator application boundary. It selects a registered
`IDeviceCommandTransport`, applies the master hardware safety switch, waits for an ACK and only
then updates the device state.

Command intent is persisted before a physical adapter is called. The same command id is upserted
with its final acknowledgement and accompanied by an audit record. Missing
transports, disabled simulation, disabled hardware control, timeouts and transport exceptions
produce a failed/blocked command without changing the reported device state.

The service never blindly retries after dispatch because the physical output may already have
changed. A persistence error after ACK is logged as critical for operator reconciliation.

Physical adapters are intentionally outside this service. See `Devices/README.md` and
`Docs/HARDWARE-INTEGRATION.md`.

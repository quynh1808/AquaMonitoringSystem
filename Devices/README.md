# Device command transports

`IDeviceCommandTransport` is the only boundary between AquaOps business logic and physical
actuators. The application currently registers `SimulatedDeviceCommandTransport`.

A production adapter must:

1. expose a unique `Name` (`mqtt-relay`, `modbus-tcp-coil`, ...);
2. validate the device `ExternalAddress` mapping;
3. honor the cancellation token and configured timeout;
4. return success only after ACK or state read-back;
5. leave emergency stop, overload and dry-run protection inside PLC/local relay logic.

See `Docs/HARDWARE-INTEGRATION.md` for the commissioning sequence.


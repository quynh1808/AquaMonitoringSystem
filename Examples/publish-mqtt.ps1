param(
    [string]$Broker = "localhost",
    [int]$Port = 1883,
    [string]$Topic = "aquaculture/farm_main/pond_01/nodes/sensor-node-01/sensors/sensor_pond_01_dissolvedOxygen/telemetry"
)

$payload = Get-Content "$PSScriptRoot/mqtt-payload.json" -Raw
mosquitto_pub -h $Broker -p $Port -t $Topic -m $payload

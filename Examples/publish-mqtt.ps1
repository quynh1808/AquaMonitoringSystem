param(
    [string]$Broker = "localhost",
    [int]$Port = 1883,
    [string]$Topic = "aquaculture/farm_main/pond_01/sensors/do_01/telemetry"
)

$payload = Get-Content "$PSScriptRoot/mqtt-payload.json" -Raw
mosquitto_pub -h $Broker -p $Port -t $Topic -m $payload

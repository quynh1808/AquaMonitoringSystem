[CmdletBinding()]
param(
    [string]$ConfigPath = "",
    [string]$Port = "",
    [switch]$ValidateOnly,
    [switch]$NoRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot "provisioning.local.json"
}

function Get-RequiredText {
    param(
        [Parameter(Mandatory)]$Configuration,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$MaximumUtf8Bytes
    )

    $property = $Configuration.PSObject.Properties[$Name]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Thiếu giá trị bắt buộc '$Name' trong $ConfigPath."
    }

    $value = [string]$property.Value
    if ($value.IndexOf([char]0) -ge 0 -or $value.Contains([char]10) -or $value.Contains([char]13)) {
        throw "'$Name' không được chứa NUL hoặc ký tự xuống dòng."
    }

    $byteCount = [Text.Encoding]::UTF8.GetByteCount($value)
    if ($byteCount -gt $MaximumUtf8Bytes) {
        throw "'$Name' dài $byteCount byte UTF-8; giới hạn firmware là $MaximumUtf8Bytes byte."
    }

    return $value
}

function Protect-DeviceOutput {
    param(
        [string]$Text,
        [string[]]$SensitiveValues
    )

    $safe = $Text
    foreach ($value in ($SensitiveValues | Where-Object { -not [string]::IsNullOrEmpty($_) } |
        Sort-Object Length -Descending -Unique)) {
        $safe = $safe.Replace($value, "***")
    }
    return $safe.Trim()
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Không tìm thấy file cấu hình '$ConfigPath'. Copy provisioning.example.json thành provisioning.local.json trước."
}

try {
    $configuration = Get-Content -Raw -LiteralPath $ConfigPath | ConvertFrom-Json
}
catch {
    throw "JSON không hợp lệ trong '$ConfigPath': $($_.Exception.Message)"
}

$effectivePort = if ([string]::IsNullOrWhiteSpace($Port)) {
    Get-RequiredText -Configuration $configuration -Name "serialPort" -MaximumUtf8Bytes 32
}
else {
    $Port
}

if ($effectivePort -notmatch "^COM[1-9][0-9]*$") {
    throw "Serial port '$effectivePort' không hợp lệ trên Windows. Ví dụ hợp lệ: COM5."
}

try {
    $baudRate = [int]$configuration.baudRate
}
catch {
    throw "'baudRate' phải là số nguyên, ví dụ 115200."
}
if ($baudRate -le 0) {
    throw "'baudRate' phải lớn hơn 0."
}

# These byte limits mirror aqua_bootstrap_config_t and the ESP-IDF Wi-Fi fields.
$wifiSsid = Get-RequiredText $configuration "wifiSsid" 32
$wifiPassword = Get-RequiredText $configuration "wifiPassword" 64
$mqttUri = Get-RequiredText $configuration "mqttUri" 128
$mqttUsername = Get-RequiredText $configuration "mqttUsername" 64
$mqttPassword = Get-RequiredText $configuration "mqttPassword" 64
$farmId = Get-RequiredText $configuration "farmId" 48
$pondId = Get-RequiredText $configuration "pondId" 48
$nodeId = Get-RequiredText $configuration "nodeId" 48

$parsedUri = $null
if (-not [Uri]::TryCreate($mqttUri, [UriKind]::Absolute, [ref]$parsedUri) -or
    @("mqtt", "mqtts") -notcontains $parsedUri.Scheme -or
    [string]::IsNullOrWhiteSpace($parsedUri.Host) -or
    $parsedUri.Port -lt 1 -or $parsedUri.Port -gt 65535) {
    throw "'mqttUri' phải là URI tuyệt đối có port dạng mqtt://host:port hoặc mqtts://host:port."
}

foreach ($identity in @(
    @{ Name = "farmId"; Value = $farmId },
    @{ Name = "pondId"; Value = $pondId },
    @{ Name = "nodeId"; Value = $nodeId }
)) {
    if ($identity.Value -notmatch "^[A-Za-z0-9][A-Za-z0-9._-]*$") {
        throw "'$($identity.Name)' chỉ được chứa chữ ASCII, số, dấu chấm, gạch dưới hoặc gạch ngang."
    }
}

if ($farmId -ne "farm_main") {
    Write-Warning "Server hiện dựng MQTT topic với farm_main. farmId '$farmId' có thể làm message bị quarantine."
}

$allValues = @(
    $wifiSsid, $wifiPassword, $mqttUri, $mqttUsername, $mqttPassword,
    $farmId, $pondId, $nodeId
)
$placeholderValues = @($allValues | Where-Object { $_ -match "^(THAY_|CHANGE_ME|REPLACE_)" })
if ($placeholderValues.Count -gt 0) {
    if (-not $ValidateOnly) {
        throw "Cấu hình vẫn chứa giá trị mẫu THAY_/CHANGE_ME/REPLACE_. Hãy sửa provisioning.local.json."
    }
    Write-Warning "Cấu hình còn giá trị mẫu; chỉ kiểm tra cấu trúc, chưa được phép ghi xuống ESP32."
}

$confirmationProperty = $configuration.PSObject.Properties["confirmProvisioning"]
if ($null -eq $confirmationProperty -or $confirmationProperty.Value -isnot [bool]) {
    throw "'confirmProvisioning' phải là JSON boolean true hoặc false, không đặt trong dấu nháy."
}
$isConfirmed = [bool]$confirmationProperty.Value

Write-Host "Cấu hình hợp lệ: Port=$effectivePort Baud=$baudRate Node=$nodeId Farm=$farmId Pond=$pondId"
Write-Host "MQTT endpoint: $($parsedUri.Scheme)://$($parsedUri.Host):$($parsedUri.Port)"

if ($ValidateOnly) {
    if (-not $isConfirmed) {
        Write-Host "confirmProvisioning=false: file chưa được mở khóa để ghi xuống thiết bị."
    }
    exit 0
}

if (-not $isConfirmed) {
    throw "Đổi confirmProvisioning thành true sau khi kiểm tra đúng board, COM port và mọi giá trị."
}

$commands = @(
    @{ Key = "wifi_ssid"; Value = $wifiSsid },
    @{ Key = "wifi_pass"; Value = $wifiPassword },
    @{ Key = "mqtt_uri"; Value = $mqttUri },
    @{ Key = "mqtt_user"; Value = $mqttUsername },
    @{ Key = "mqtt_pass"; Value = $mqttPassword },
    @{ Key = "farm_id"; Value = $farmId },
    @{ Key = "pond_id"; Value = $pondId },
    @{ Key = "node_id"; Value = $nodeId }
)

$serial = $null
try {
    $serial = [IO.Ports.SerialPort]::new(
        $effectivePort,
        $baudRate,
        [IO.Ports.Parity]::None,
        8,
        [IO.Ports.StopBits]::One
    )
    $serial.Handshake = [IO.Ports.Handshake]::None
    $serial.NewLine = [string][char]10
    $serial.Encoding = [Text.UTF8Encoding]::new($false)
    $serial.ReadTimeout = 500
    $serial.WriteTimeout = 2000
    $serial.DtrEnable = $false
    $serial.RtsEnable = $false

    Write-Host "Đang mở $effectivePort. Hãy đóng PlatformIO Serial Monitor nếu cổng đang bị chiếm."
    $serial.Open()

    # Opening a USB-UART port may reset some ESP32 boards. Give app_main and the
    # provisioning task enough time to initialize before sending the first line.
    Start-Sleep -Milliseconds 2200
    if ($serial.BytesToRead -gt 0) {
        $serial.DiscardInBuffer()
    }

    foreach ($item in $commands) {
        $serial.WriteLine("set $($item.Key) $($item.Value)")
        $serial.BaseStream.Flush()
        Start-Sleep -Milliseconds 500

        $response = Protect-DeviceOutput -Text $serial.ReadExisting() -SensitiveValues $allValues
        if (-not [string]::IsNullOrWhiteSpace($response)) {
            Write-Host $response
        }
        Write-Host "Đã gửi và chờ lưu: $($item.Key)"
    }

    $serial.WriteLine("status")
    $serial.BaseStream.Flush()
    Start-Sleep -Milliseconds 750
    $statusOutput = Protect-DeviceOutput -Text $serial.ReadExisting() -SensitiveValues $allValues
    if (-not [string]::IsNullOrWhiteSpace($statusOutput)) {
        Write-Host $statusOutput
    }

    if ($NoRestart) {
        Write-Warning "Đã lưu NVS nhưng chưa restart. Firmware tiếp tục dùng cấu hình runtime cũ cho đến lần khởi động kế tiếp."
    }
    else {
        Write-Host "Đang yêu cầu ESP32 restart để dùng cấu hình mới."
        $serial.WriteLine("restart")
        $serial.BaseStream.Flush()
        Start-Sleep -Milliseconds 300
    }
}
catch {
    throw "Provisioning qua $effectivePort thất bại: $($_.Exception.Message)"
}
finally {
    if ($null -ne $serial) {
        if ($serial.IsOpen) {
            $serial.Close()
        }
        $serial.Dispose()
    }
}

Write-Host "Provisioning hoàn tất. Có thể mở Serial Monitor để xem Wi-Fi/MQTT reconnect nếu cần."

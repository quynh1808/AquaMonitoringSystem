param([string]$Destination = "backups/postgresql")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (-not (Test-Path .env)) { throw "Missing .env." }
$values = @{}
Get-Content .env | Where-Object { $_ -match '^[A-Za-z_][A-Za-z0-9_]*=' } | ForEach-Object {
    $key, $value = $_ -split '=', 2
    $values[$key] = $value.Trim().Trim("'").Trim('"')
}
foreach ($key in 'POSTGRES_DB','POSTGRES_APP_USERNAME','POSTGRES_APP_PASSWORD') {
    if ([string]::IsNullOrWhiteSpace($values[$key])) { throw "$key is required in .env." }
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$localFile = Join-Path $Destination "aquaops-$stamp.dump"
$containerFile = "/tmp/aquaops-$stamp.dump"
$containerId = (docker compose ps -q postgresql).Trim()
if (-not $containerId) { throw "PostgreSQL container is not running." }
try {
    # PGPASSWORD exists only for the pg_dump child process and is cleared even if Docker fails.
    $env:PGPASSWORD = $values.POSTGRES_APP_PASSWORD
    docker compose exec -T -e PGPASSWORD postgresql pg_dump `
        --username $values.POSTGRES_APP_USERNAME --dbname $values.POSTGRES_DB `
        --format custom --no-owner --no-privileges --file $containerFile
    if ($LASTEXITCODE -ne 0) { throw "pg_dump failed." }
    docker cp "${containerId}:${containerFile}" $localFile
    if ($LASTEXITCODE -ne 0) { throw "docker cp failed." }
}
finally {
    $env:PGPASSWORD = $null
    docker compose exec -T postgresql rm -f $containerFile 2>$null
}
if (-not (Test-Path $localFile) -or (Get-Item $localFile).Length -eq 0) { throw "Backup is empty." }
Write-Host "PostgreSQL backup created: $localFile"

param([Parameter(Mandatory)][string]$BackupFile)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
$resolvedBackup = (Resolve-Path $BackupFile).Path
if (-not (Test-Path .env)) { throw "Missing .env." }
$values = @{}
Get-Content .env | Where-Object { $_ -match '^[A-Za-z_][A-Za-z0-9_]*=' } | ForEach-Object {
    $key, $value = $_ -split '=', 2
    $values[$key] = $value.Trim().Trim("'").Trim('"')
}
foreach ($key in 'POSTGRES_DB','POSTGRES_APP_USERNAME','POSTGRES_APP_PASSWORD') {
    if ([string]::IsNullOrWhiteSpace($values[$key])) { throw "$key is required in .env." }
}
$confirmation = Read-Host "Restore replaces objects in '$($values.POSTGRES_DB)'. Type RESTORE"
if ($confirmation -cne 'RESTORE') { Write-Host 'Cancelled.'; exit 1 }
$containerId = (docker compose ps -q postgresql).Trim()
if (-not $containerId) { throw "PostgreSQL container is not running." }
$containerFile = "/tmp/aquaops-restore.dump"
docker compose stop aquaops
docker cp $resolvedBackup "${containerId}:${containerFile}"
if ($LASTEXITCODE -ne 0) { throw "docker cp failed; AquaOps remains stopped for inspection." }
try {
    # Keep AquaOps stopped on restore failure; an incomplete database must not serve traffic.
    $env:PGPASSWORD = $values.POSTGRES_APP_PASSWORD
    docker compose exec -T -e PGPASSWORD postgresql pg_restore `
        --username $values.POSTGRES_APP_USERNAME --dbname $values.POSTGRES_DB `
        --clean --if-exists --no-owner --no-privileges $containerFile
    if ($LASTEXITCODE -ne 0) { throw "pg_restore failed; AquaOps remains stopped for inspection." }
}
finally {
    $env:PGPASSWORD = $null
    docker compose exec -T postgresql rm -f $containerFile 2>$null
}
docker compose up -d aquaops
Write-Host 'Restore completed. Verify /health/ready and operational totals.'

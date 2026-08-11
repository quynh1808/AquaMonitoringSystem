param(
    [string]$BackupRoot = ".\backups",
    [int]$RetentionDays = 30
)
$ErrorActionPreference = "Stop"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$destination = Join-Path $BackupRoot $stamp
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$archive = Join-Path $destination "aquaculture_monitoring.archive.gz"

$command = 'docker compose exec -T mongodb sh -lc ''mongodump --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --db "${MONGO_APP_DATABASE:-aquaculture_monitoring}" --archive --gzip'''
cmd /c "$command > `"$archive`""
if ($LASTEXITCODE -ne 0) { throw "mongodump thất bại." }

Get-ChildItem $BackupRoot -Directory | Where-Object LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) | Remove-Item -Recurse -Force
Write-Host "Backup: $archive"

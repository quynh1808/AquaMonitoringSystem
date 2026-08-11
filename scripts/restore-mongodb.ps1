param([Parameter(Mandatory = $true)][string]$Archive)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $Archive)) { throw "Không tìm thấy file: $Archive" }

$command = 'docker compose exec -T mongodb sh -lc ''mongorestore --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --db "${MONGO_APP_DATABASE:-aquaculture_monitoring}" --drop --archive --gzip'''
cmd /c "$command < `"$Archive`""
if ($LASTEXITCODE -ne 0) { throw "mongorestore thất bại." }
Write-Host "Đã phục hồi $Archive"

param(
    [switch]$Docker
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$username = Read-Host 'Administrator username (default: admin)'
if ([string]::IsNullOrWhiteSpace($username)) { $username = 'admin' }
$securePassword = Read-Host 'New password (at least 12 characters)' -AsSecureString
$secureConfirmation = Read-Host 'Confirm new password' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$confirmationPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureConfirmation)

try {
    # Plain text exists only for the lifetime of this process because both the application and
    # Docker require an environment string. It is not placed in command arguments or a file.
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $plainConfirmation = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($confirmationPointer)
    if ($plainPassword.Length -lt 12) { throw 'Password must contain at least 12 characters.' }
    if ($plainPassword -cne $plainConfirmation) { throw 'Password confirmation does not match.' }
    $env:AQUAOPS_RESET_USERNAME = $username
    $env:AQUAOPS_RESET_PASSWORD = $plainPassword

    Push-Location $projectRoot
    try {
        if ($Docker) {
            docker compose run --rm --no-deps -e AQUAOPS_RESET_USERNAME -e AQUAOPS_RESET_PASSWORD aquaops reset-admin-password
        }
        else {
            dotnet run --project AquacultureMonitoring.csproj -- reset-admin-password
        }
        if ($LASTEXITCODE -ne 0) { throw "Password reset command failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}
finally {
    $env:AQUAOPS_RESET_PASSWORD = $null
    $env:AQUAOPS_RESET_USERNAME = $null
    $plainPassword = $null
    $plainConfirmation = $null
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($confirmationPointer)
}

Write-Host 'Administrator password reset completed.'

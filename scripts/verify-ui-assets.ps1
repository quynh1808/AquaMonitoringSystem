param(
    [string]$BaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"
$targets = @(
    @{ Path = "/"; Expected = "text/html" },
    @{ Path = "/login"; Expected = "text/html" },
    @{ Path = "/app.css"; Expected = "text/css" },
    @{ Path = "/app.js"; Expected = "javascript" },
    @{ Path = "/_framework/blazor.web.js"; Expected = "javascript" }
)

$failed = $false
foreach ($target in $targets) {
    $uri = "$BaseUrl$($target.Path)"
    try {
        $response = Invoke-WebRequest -Uri $uri -MaximumRedirection 0 -SkipHttpErrorCheck
        $contentType = [string]$response.Headers["Content-Type"]
        $ok = $response.StatusCode -eq 200 -and $contentType.Contains($target.Expected)
        $status = if ($ok) { "OK" } else { "FAIL" }
        Write-Host ("{0,-5} {1,-35} HTTP {2}  {3}" -f $status, $target.Path, $response.StatusCode, $contentType)
        if (-not $ok) { $failed = $true }
    }
    catch {
        Write-Host ("FAIL  {0,-35} {1}" -f $target.Path, $_.Exception.Message)
        $failed = $true
    }
}

if ($failed) { exit 1 }

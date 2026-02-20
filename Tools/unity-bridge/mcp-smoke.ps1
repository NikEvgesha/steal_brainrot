param(
    [string]$BaseUrl = "http://localhost:7777"
)

$ErrorActionPreference = "Stop"
$wrapper = Join-Path $PSScriptRoot "mcp-wrapper.ps1"

if (-not (Test-Path $wrapper)) {
    Write-Error "Wrapper not found: $wrapper"
    exit 1
}

$tests = @(
    @{
        Name = "ping"
        Args = @("-Command", "ping", "-BaseUrl", $BaseUrl)
    },
    @{
        Name = "scene_hierarchy"
        Args = @("-Command", "scene_hierarchy", "-BaseUrl", $BaseUrl)
    },
    @{
        Name = "execute_unity_version"
        Args = @("-Command", "execute", "-BaseUrl", $BaseUrl, "-Code", "return UnityEngine.Application.unityVersion;")
    }
)

$results = @()
foreach ($test in $tests) {
    try {
        $output = & powershell -ExecutionPolicy Bypass -File $wrapper @($test.Args) 2>&1
        $results += [PSCustomObject]@{
            Test = $test.Name
            Status = "PASS"
            Output = ($output -join "`n")
        }
    }
    catch {
        $results += [PSCustomObject]@{
            Test = $test.Name
            Status = "FAIL"
            Output = $_.Exception.Message
        }
    }
}

$failed = $results | Where-Object { $_.Status -eq "FAIL" }
foreach ($r in $results) {
    if ($r.Status -eq "PASS") {
        Write-Host "[PASS] $($r.Test)" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] $($r.Test)" -ForegroundColor Red
    }
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed tests details:" -ForegroundColor Yellow
    foreach ($f in $failed) {
        Write-Host "---- $($f.Test) ----"
        Write-Host $f.Output
    }
    exit 1
}

Write-Host ""
Write-Host "Smoke checks completed successfully." -ForegroundColor Green

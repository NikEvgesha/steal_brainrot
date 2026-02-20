param(
    [ValidateSet("ping", "scene_hierarchy", "scene_grep", "screenshot", "camera_screenshot", "execute", "raw")]
    [string]$Command = "ping",
    [string]$BaseUrl = "http://localhost:7777",
    [string]$Query,
    [string]$Code,
    [string]$Json = "{}",
    [string]$Endpoint = "/api/execute",
    [string]$OutFile = "Tools/unity-bridge/last-screenshot.png",
    [int]$Width = 1280,
    [int]$Height = 720,
    [float[]]$Position = @(0, 2, -5),
    [float[]]$Target = @(0, 0, 0),
    [int]$Fov = 60
)

function Invoke-McpPost {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][object]$Body
    )

    $payload = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 32 -Compress }
    return Invoke-RestMethod -Uri $Url -Method Post -ContentType "application/json" -Body $payload -TimeoutSec 30
}

function Save-Base64Image {
    param(
        [Parameter(Mandatory = $true)][string]$Base64,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    [System.IO.File]::WriteAllBytes($Path, [Convert]::FromBase64String($Base64))
}

try {
    switch ($Command) {
        "ping" {
            $resp = Invoke-RestMethod -Uri "$BaseUrl/" -Method Get -TimeoutSec 10
            Write-Host "Bridge reachable at $BaseUrl"
            $resp | ConvertTo-Json -Depth 8
        }
        "scene_hierarchy" {
            $resp = Invoke-McpPost -Url "$BaseUrl/api/scene_hierarchy" -Body @{}
            $resp | ConvertTo-Json -Depth 32
        }
        "scene_grep" {
            if ([string]::IsNullOrWhiteSpace($Query)) {
                throw "Use -Query for scene_grep. Example: -Query name~Player"
            }
            $resp = Invoke-McpPost -Url "$BaseUrl/api/scene_grep" -Body @{ query = $Query }
            $resp | ConvertTo-Json -Depth 32
        }
        "screenshot" {
            $resp = Invoke-McpPost -Url "$BaseUrl/api/screenshot" -Body @{
                width = $Width
                height = $Height
                view_type = "game"
            }
            if ($resp.image) {
                Save-Base64Image -Base64 $resp.image -Path $OutFile
                Write-Host "Saved screenshot: $OutFile"
            }
            $resp | ConvertTo-Json -Depth 16
        }
        "camera_screenshot" {
            $resp = Invoke-McpPost -Url "$BaseUrl/api/camera_screenshot" -Body @{
                position = $Position
                target = $Target
                fov = $Fov
                width = $Width
                height = $Height
            }
            if ($resp.image) {
                Save-Base64Image -Base64 $resp.image -Path $OutFile
                Write-Host "Saved camera screenshot: $OutFile"
            }
            $resp | ConvertTo-Json -Depth 16
        }
        "execute" {
            if ([string]::IsNullOrWhiteSpace($Code)) {
                throw "Use -Code for execute."
            }
            $resp = Invoke-McpPost -Url "$BaseUrl/api/execute" -Body @{
                code = $Code
                taskId = [Guid]::NewGuid().ToString()
            }
            $resp | ConvertTo-Json -Depth 32
        }
        "raw" {
            $resp = Invoke-McpPost -Url "$BaseUrl$Endpoint" -Body $Json
            $resp | ConvertTo-Json -Depth 32
        }
    }
}
catch {
    Write-Error $_
    exit 1
}

# Build Multiplayer Chat and create release zip
# Zip structure: MultiplayerChat-{version}/manifest.json, Plugins/MultiplayerChat.dll, Tools/SlzMarkerTool.exe
# CAU is distributed separately.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Get version from manifest
$manifest = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version
$zipName = "MultiplayerChat-$version.zip"
$folderName = "MultiplayerChat-$version"

# Build mod + SLZ marker helper
Write-Host "Building MultiplayerChat..."
dotnet build "$root\MultiplayerChat.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "Building SlzMarkerTool..."
dotnet build "$root\SlzMarkerTool\SlzMarkerTool.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Create zip structure
$tempDir = Join-Path $env:TEMP $folderName
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path "$tempDir\Plugins" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempDir\Plugins\Sounds" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempDir\Plugins\MultiplayerChat\Sounds" -Force | Out-Null
New-Item -ItemType Directory -Path "$tempDir\Tools" -Force | Out-Null

# Prefer releases\ layout from csproj (same as manual install folder); fallback to bin\Release.
$dllSource = if (Test-Path "$root\releases\Plugins\MultiplayerChat.dll") { "$root\releases\Plugins\MultiplayerChat.dll" } else { "$root\bin\Release\MultiplayerChat.dll" }
$manifestSource = if (Test-Path "$root\releases\manifest.json") { "$root\releases\manifest.json" } else { "$root\manifest.json" }
Copy-Item $manifestSource "$tempDir\"
Copy-Item $dllSource "$tempDir\Plugins\"

$slzExe = "$root\SlzMarkerTool\bin\Release\SlzMarkerTool.exe"
if (!(Test-Path $slzExe)) { Write-Error "SlzMarkerTool.exe not found at $slzExe" }
Copy-Item $slzExe "$tempDir\Tools\"

$soundsSrc = if (Test-Path "$root\releases\Plugins\Sounds") { "$root\releases\Plugins\Sounds" } else { "$root\Sounds" }
Copy-Item "$soundsSrc\*.ogg" "$tempDir\Plugins\Sounds\" -ErrorAction SilentlyContinue
Copy-Item "$soundsSrc\*.ogg" "$tempDir\Plugins\MultiplayerChat\Sounds\" -ErrorAction SilentlyContinue

# Create zip
$zipPath = Join-Path $root $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $tempDir -DestinationPath $zipPath -Force
Remove-Item $tempDir -Recurse -Force

Write-Host "Created: $zipPath"
Get-Item $zipPath | Format-List FullName, Length

# Build Multiplayer Chat and create release zip
# Zip structure: MultiplayerChat-{version}/manifest.json, Plugins/MultiplayerChat.dll
# CAU is distributed separately.

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Get version from manifest
$manifest = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version
$zipName = "MultiplayerChat-$version.zip"
$folderName = "MultiplayerChat-$version"

# Build mod only
Write-Host "Building MultiplayerChat..."
dotnet build "$root\MultiplayerChat.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Create zip structure
$tempDir = Join-Path $env:TEMP $folderName
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path "$tempDir\Plugins" -Force | Out-Null

Copy-Item "$root\manifest.json" "$tempDir\"
Copy-Item "$root\bin\Release\MultiplayerChat.dll" "$tempDir\Plugins\"

# Create zip
$zipPath = Join-Path $root $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $tempDir -DestinationPath $zipPath -Force
Remove-Item $tempDir -Recurse -Force

Write-Host "Created: $zipPath"
Get-Item $zipPath | Format-List FullName, Length

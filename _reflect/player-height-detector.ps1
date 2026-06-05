$bs = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)'
$managed = Join-Path $bs 'Beat Saber_Data\Managed'
$handler = {
    param($sender, $e)
    $name = ($e.Name -replace ',.*$','')
    $path = Join-Path $managed ($name + '.dll')
    if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$main = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Main.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
foreach ($name in @('PlayerHeightDetector', 'Gameplay.PlayerHeightDetector')) {
    $t = $main.GetType($name)
    if ($t) { Write-Host "found $name as $($t.FullName)" }
}
$t = $main.GetType('PlayerHeightDetector')
if (-not $t) { $t = $main.GetTypes() | Where-Object { $_.Name -eq 'PlayerHeightDetector' } | Select-Object -First 1 }
if ($t) {
    Write-Host "=== $($t.FullName) ==="
    $t.GetMethods($flags) | Where-Object { $_.Name -match 'Height|Detect|Compute|Measure|get_' } | ForEach-Object { $_.ToString() } | Select-Object -First 25
    $t.GetProperties($flags) | ForEach-Object { $_.ToString() }
}

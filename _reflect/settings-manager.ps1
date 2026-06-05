$bs = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)'
$plugins = Join-Path $bs 'Plugins'
$managed = Join-Path $bs 'Beat Saber_Data\Managed'
$handler = {
    param($sender, $e)
    $name = ($e.Name -replace ',.*$','')
    foreach ($dir in @($managed, $plugins)) {
        $path = Join-Path $dir ($name + '.dll')
        if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    }
    $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $plugins 'CustomAvatar.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
foreach ($name in @('CustomAvatar.Configuration.SettingsManager', 'CustomAvatar.Settings.SettingsManager', 'CustomAvatar.Player.SettingsManager')) {
    $t = $asm.GetType($name)
    if ($null -eq $t) { Write-Host "MISSING $name"; continue }
    Write-Host "=== $name ==="
    $t.GetMethods($flags) | ForEach-Object { $_.ToString() } | Select-Object -First 40
    $t.GetProperties($flags) | ForEach-Object { $_.ToString() }
}
$svc = $asm.GetType('CustomAvatar.UI.SettingsViewController')
$svc.GetMethods($flags) | Where-Object { $_.Name -match 'Measure' } | ForEach-Object { "SVC $($_.ToString())" }

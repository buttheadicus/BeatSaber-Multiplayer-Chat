$bs = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)'
$plugins = Join-Path $bs 'Plugins'
$managed = Join-Path $bs 'Beat Saber_Data\Managed'
$libs = Join-Path $bs 'Libs'
$handler = {
    param($sender, $e)
    $name = ($e.Name -replace ',.*$','')
    foreach ($dir in @($managed, $plugins, $libs)) {
        $path = Join-Path $dir ($name + '.dll')
        if (Test-Path $path) { return [Reflection.Assembly]::LoadFrom($path) }
    }
    $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $plugins 'CustomAvatar.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
foreach ($name in @(
    'CustomAvatar.UI.GeneralSettingsHost',
    'CustomAvatar.UI.AvatarSpecificSettingsHost',
    'CustomAvatar.Configuration.Settings'
)) {
    $t = $asm.GetType($name)
    if ($null -eq $t) { Write-Host "MISSING $name"; continue }
    Write-Host "=== $name ==="
    $t.GetMethods($flags) | Where-Object { $_.Name -match 'Measure|Height|Eye|Player' } | ForEach-Object { $_.ToString() }
    $t.GetProperties($flags) | Where-Object { $_.Name -match 'height|Height|eye|Eye|player|Player' } | ForEach-Object { $_.ToString() }
}

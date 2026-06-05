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
$hostType = $asm.GetType('CustomAvatar.UI.GeneralSettingsHost')
$hostType.GetMethods($flags) | Where-Object { $_.Name -match 'Measure|Height|Eye|Player|Arm' } | ForEach-Object { $_.ToString() }
$hostType.GetFields($flags) | Where-Object { $_.Name -match 'manager|settings|height|Height' } | ForEach-Object { $_.ToString() }

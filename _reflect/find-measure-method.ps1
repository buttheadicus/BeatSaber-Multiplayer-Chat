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
foreach ($n in @(
    'CustomAvatar.UI.GeneralSettingsHost',
    'CustomAvatar.Player.PlayerAvatarManager',
    'CustomAvatar.Tracking.TrackingRig'
)) {
    $t = $asm.GetType($n)
    $m = $t.GetMethods($flags) | Where-Object { $_.Name -eq 'OnMeasureHeightButtonClicked' }
    if ($m) {
        Write-Host "FOUND on $n"
        $m | ForEach-Object { $_.ToString() }
    }
}
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
$pam.GetMethods($flags) | Where-Object { $_.Name -eq 'GetEyeHeight' } | ForEach-Object { $_.ToString() }

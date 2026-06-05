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
$rig = $asm.GetType('CustomAvatar.Tracking.TrackingRig')
$rig.GetProperties($flags) | Where-Object { $_.Name -match 'head|eye|height|Height' } | ForEach-Object { $_.ToString() }
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
$pam.GetMethods($flags) | Where-Object { $_.Name -match 'Eye|Height|Resize' } | ForEach-Object { $_.ToString() }
$obs = $asm.GetType('CustomAvatar.Configuration.ObservableValue`1')
if ($obs) { Write-Host "ObservableValue generic: $($obs.FullName)" }
$obsSingle = $asm.GetType('CustomAvatar.Configuration.ObservableValue`1').MakeGenericType([single])
$obsSingle.GetMembers($flags) | Where-Object { $_.Name -match 'value|Value' } | ForEach-Object { $_.ToString() }

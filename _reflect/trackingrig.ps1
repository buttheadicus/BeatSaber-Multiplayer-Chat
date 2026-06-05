$bs = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)'
$managed = Join-Path $bs 'Beat Saber_Data\Managed'
$plugins = Join-Path $bs 'Plugins'
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
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
$pam.GetFields($flags) | Where-Object { $_.Name -match 'rig|track|calibr|spawn' } | ForEach-Object { $_.ToString() }
$pam.GetProperties($flags) | Where-Object { $_.Name -match 'rig|track|calibr|spawn' } | ForEach-Object { $_.ToString() }
$rig = $asm.GetType('CustomAvatar.Tracking.TrackingRig')
$rig.GetMethods($flags) | Where-Object { $_.Name -match 'Begin|End|Calibrat|Instance|get_' } | Select-Object -First 20 | ForEach-Object { $_.ToString() }
$pam.GetMethods($flags) | Where-Object { $_.Name -match 'TrackingRig|rig|BeginCalibration' } | ForEach-Object { $_.ToString() }
$spawned = $asm.GetType('CustomAvatar.Avatar.SpawnedAvatar')
$spawned.GetFields($flags) | Where-Object { $_.Name -match 'rig|track' } | ForEach-Object { $_.ToString() }
$spawned.GetProperties($flags) | Where-Object { $_.Name -match 'rig|track' } | ForEach-Object { $_.ToString() }

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
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
$pam.GetMethods($flags) | Where-Object { $_.Name -match 'Eye|Height|Resize|Scale' } | ForEach-Object { $_.ToString() }
$rig = $asm.GetType('CustomAvatar.Tracking.TrackingRig')
$rig.GetProperties($flags) | Where-Object { $_.Name -match 'eye|Eye|height|Height' } | ForEach-Object { $_.ToString() }
$gsh = $asm.GetType('CustomAvatar.UI.GeneralSettingsHost')
$gsh.BaseType.GetMethods($flags) | Where-Object { $_.Name -match 'Eye|Height|Player' } | ForEach-Object { "$($gsh.BaseType.Name) :: $($_.ToString())" }

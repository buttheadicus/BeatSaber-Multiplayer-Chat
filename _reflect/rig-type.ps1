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
$t = $asm.GetType('CustomAvatar.Tracking.TrackingRig')
Write-Host "Base: $($t.BaseType.FullName)"
Write-Host "Is Object: $([UnityEngine.Object].IsAssignableFrom($t))"
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
Write-Host "PAM Base: $($pam.BaseType.FullName)"
$pam.GetProperties([Reflection.BindingFlags]'Instance,Public,NonPublic') | Where-Object { $_.Name -match 'tracking|Tracking|rig|Rig|settings|Settings' } | ForEach-Object { $_.ToString() }
$pam.GetFields([Reflection.BindingFlags]'Instance,Public,NonPublic') | Where-Object { $_.Name -match 'tracking|Tracking|rig|Rig|settings' } | ForEach-Object { $_.ToString() }

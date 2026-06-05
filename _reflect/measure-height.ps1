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
$svc = $asm.GetType('CustomAvatar.UI.SettingsViewController')
Write-Host '=== SettingsViewController height methods ==='
$svc.GetMethods($flags) | Where-Object { $_.Name -match 'Measure|Height|Resize|Player|Calibrat' } | ForEach-Object { $_.ToString() }
$svc.GetFields($flags) | Where-Object { $_.Name -match 'height|Height|player|Player|manager|Manager' } | ForEach-Object { $_.ToString() }
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
Write-Host '=== PlayerAvatarManager height methods ==='
$pam.GetMethods($flags) | Where-Object { $_.Name -match 'Measure|Height|Resize|Eye|Player' } | ForEach-Object { $_.ToString() }
$getEye = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager') # search GetEyeHeight in assembly
foreach ($t in @($pam, $asm.GetType('CustomAvatar.Configuration.Settings'))) {
    if ($null -eq $t) { continue }
    $t.GetMethods($flags) | Where-Object { $_.Name -match 'GetEyeHeight|GetPlayerHeight|Measure' } | ForEach-Object { "$($t.Name) :: $($_.ToString())" }
}

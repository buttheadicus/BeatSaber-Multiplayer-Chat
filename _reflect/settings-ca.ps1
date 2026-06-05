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
foreach ($name in @('CustomAvatar.Configuration.SettingsManager', 'CustomAvatar.Configuration.Settings', 'CustomAvatar.UI.ViewControllerHost')) {
    $t = $asm.GetType($name)
    if ($null -eq $t) { Write-Host "MISSING $name"; continue }
    Write-Host "=== $name ==="
    $t.GetMembers($flags) | Where-Object { $_.ToString() -match 'height|Height|Eye|Instance|settings|manager|Manager|Measure|playerAvatar' } | ForEach-Object { $_.ToString() }
}
$pam = $asm.GetType('CustomAvatar.Player.PlayerAvatarManager')
$pam.GetMethods($flags) | Where-Object { $_.Name -match 'Eye|Height|Resize|Measure' } | ForEach-Object { $_.ToString() }

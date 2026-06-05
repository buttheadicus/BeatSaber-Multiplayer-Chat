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
foreach ($name in @(
    'CustomAvatar.UI.AvatarMenuFlowCoordinator',
    'CustomAvatar.UI.SettingsViewController',
    'CustomAvatar.Player.PlayerAvatarManager',
    'CustomAvatar.Avatar.HumanoidCalibrator',
    'CustomAvatar.Tracking.HumanoidCalibrator'
)) {
    $t = $asm.GetType($name)
    if ($null -eq $t) { Write-Host "MISSING $name"; continue }
    Write-Host "=== $name ==="
    $flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
    $t.GetMethods($flags) |
        Where-Object { $_.Name -match 'Calibrat|Resize|Measure|Height|Begin|End' } |
        ForEach-Object { $_.ToString() }
    $t.GetProperties($flags) |
        Where-Object { $_.Name -match 'Instance|singleton|manager' } |
        ForEach-Object { "PROP $($_.ToString())" }
}

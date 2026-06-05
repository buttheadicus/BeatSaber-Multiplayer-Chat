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
Write-Host "Is Object: $([bool]([UnityEngine.Object].Assembly.GetType('UnityEngine.Object').IsAssignableFrom($svc)))"
$svc.GetFields($flags) | Where-Object { $_.FieldType.Name -match 'Host|Settings' } | ForEach-Object { $_.ToString() }
$svc.GetProperties($flags) | Where-Object { $_.PropertyType.Name -match 'Host|General' } | ForEach-Object { $_.ToString() }
$hostType = $asm.GetType('CustomAvatar.UI.GeneralSettingsHost')
Write-Host "GeneralSettingsHost base: $($hostType.BaseType.FullName)"

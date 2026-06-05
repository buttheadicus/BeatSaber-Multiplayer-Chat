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
foreach ($fi in @('FinalIK','UnityEngine.CoreModule','HMUI')) {
    $p = Join-Path $managed ($fi + '.dll')
    if (Test-Path $p) { [void][Reflection.Assembly]::LoadFrom($p) }
}
$pFinal = Join-Path $plugins 'FinalIK.dll'
if (Test-Path $pFinal) { [void][Reflection.Assembly]::LoadFrom($pFinal) }

try {
    foreach ($t in $asm.GetTypes()) {
        $m = $t.GetMethods($flags) | Where-Object { $_.Name -eq 'BeginCalibration' }
        if ($m) {
            Write-Host "TYPE $($t.FullName)"
            $m | ForEach-Object { $_.ToString() }
        }
    }
} catch [Reflection.ReflectionTypeLoadException] {
    $_.Exception.LoaderExceptions | Select-Object -First 3 | ForEach-Object { $_.Message }
    foreach ($t in $_.Exception.Types) {
        if ($null -eq $t) { continue }
        $m = $t.GetMethods($flags) | Where-Object { $_.Name -eq 'BeginCalibration' }
        if ($m) {
            Write-Host "TYPE $($t.FullName)"
            $m | ForEach-Object { $_.ToString() }
        }
    }
}
$svc = $asm.GetType('CustomAvatar.UI.SettingsViewController')
$svc.GetMethods($flags) | Where-Object { $_.Name -match 'Measure|Calibrat' } | ForEach-Object { $_.ToString() }

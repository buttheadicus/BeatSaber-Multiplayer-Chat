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
foreach ($fi in @('FinalIK','DynamicBone')) {
    $p = Join-Path $plugins ($fi + '.dll')
    if (Test-Path $p) { try { [void][Reflection.Assembly]::LoadFrom($p) } catch {} }
}
$types = @()
try { $types = $asm.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types }
foreach ($t in $types) {
    if ($null -eq $t) { continue }
    foreach ($m in $t.GetMembers($flags)) {
        if ($m.MemberType -ne 'Field' -and $m.MemberType -ne 'Property') { continue }
        if ($m.ToString() -match 'TrackingRig') {
            Write-Host "$($t.FullName) :: $($m.ToString())"
        }
    }
}

$dll = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Plugins\CustomAvatar.dll'
if (-not (Test-Path $dll)) { $dll = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Plugins\CustomAvatars.dll' }
$asm = [Reflection.Assembly]::LoadFrom($dll)
$asm.GetTypes() | Where-Object { $_.Name -match 'Calibrat|Resize|Height|PlayerAvatar|FlowCoordinator' } | ForEach-Object {
    "--- $($_.FullName) ---"
    $_.GetMethods([Reflection.BindingFlags]'Instance,Static,Public,NonPublic') | Where-Object { $_.Name -match 'Calibrat|Open|Show|Start|Height' } | ForEach-Object { $_.ToString() }
}

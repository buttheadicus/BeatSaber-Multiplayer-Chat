$managed = "F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed"
$main = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Main.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$t = $main.GetType('PlayerHeightSettingsController')
Write-Host "Found: $($t.FullName)"
$t.GetProperties($flags) | ForEach-Object { $_.ToString() }
$t.GetMethods($flags) | Where-Object { $_.Name -match 'height|Height|get_|set_' } | ForEach-Object { $_.ToString() }

$managed = "F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed"
$main = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Main.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$t = $main.GetType('PlayerDataModel')
if (-not $t) { $t = $main.GetTypes() | Where-Object { $_.Name -eq 'PlayerDataModel' } | Select-Object -First 1 }
if (-not $t) { 'no PlayerDataModel'; exit }
Write-Host $t.FullName
$t.GetProperties($flags) | Where-Object { $_.Name -match 'height|Height|player' } | ForEach-Object { $_.ToString() }
$t.GetMethods($flags) | Where-Object { $_.Name -match 'height|Height' } | ForEach-Object { $_.ToString() }

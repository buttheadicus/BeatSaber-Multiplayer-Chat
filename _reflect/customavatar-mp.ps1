$dll = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Plugins\CustomAvatar.dll'
$asm = [Reflection.Assembly]::LoadFrom($dll)
$asm.GetTypes() | Where-Object {
    $_.FullName -match 'Multiplayer|GameCore|GameAvatar|Connected'
} | ForEach-Object { $_.FullName }

$asm = [Reflection.Assembly]::LoadFrom('F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\Main.dll')
$t = $asm.GetType('MultiplayerConnectedPlayerFacade')
$t.GetInterfaces() | ForEach-Object { $_.FullName }
'---fields---'
$t.GetFields([Reflection.BindingFlags]'Instance,Public,NonPublic') | ForEach-Object { "$($_.FieldType.Name) $($_.Name)" }
'---props---'
$t.GetProperties([Reflection.BindingFlags]'Instance,Public,NonPublic') | ForEach-Object { "$($_.PropertyType.Name) $($_.Name)" }

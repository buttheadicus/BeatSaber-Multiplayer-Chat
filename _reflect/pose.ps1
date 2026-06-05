$asm = [Reflection.Assembly]::LoadFrom('F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\BeatSaber.AvatarCore.dll')
$t = $asm.GetType('BeatSaber.AvatarCore.MultiplayerAvatarPoseController')
$t.GetProperties() | ForEach-Object { "$($_.PropertyType.Name) $($_.Name)" }
'---fields---'
$t.GetFields([Reflection.BindingFlags]'Instance,Public,NonPublic') | ForEach-Object { "$($_.FieldType.Name) $($_.Name)" }

$asm = [Reflection.Assembly]::LoadFrom('F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\BeatSaber.AvatarCore.dll')
$t = $asm.GetType('BeatSaber.AvatarCore.MultiplayerAvatarPoseController')
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$t.GetMethods($flags) | ForEach-Object { $_.ToString() }
$t.GetProperties($flags) | ForEach-Object { $_.ToString() }

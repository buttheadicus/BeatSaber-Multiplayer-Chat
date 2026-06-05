$asm = [Reflection.Assembly]::LoadFrom('F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\Main.dll')
$asm.GetTypes() | Where-Object { $_.Name -like '*Duel*' } | Select-Object -First 30 Name

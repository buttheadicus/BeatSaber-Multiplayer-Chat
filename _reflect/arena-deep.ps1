$main = [Reflection.Assembly]::LoadFrom('F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\Main.dll')
$facade = $main.GetType('MultiplayerConnectedPlayerFacade')
$facade.GetMethods([Reflection.BindingFlags]'Instance,Public,NonPublic') | Where-Object { $_.Name -like '*Big*' -or $_.Name -like '*Avatar*' -or $_.Name -like '*Hide*' } | ForEach-Object { "$($_.ReturnType.Name) $($_.Name)" }

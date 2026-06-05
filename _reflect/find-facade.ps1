$dir = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed'
Get-ChildItem $dir -Filter '*.dll' | ForEach-Object {
    try {
        $asm = [Reflection.Assembly]::LoadFrom($_.FullName)
        $t = $asm.GetType('MultiplayerConnectedPlayerFacade', $false)
        if ($t) { Write-Output $_.Name }
    } catch {}
}

$managed = 'F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed'
Get-ChildItem $managed -Filter '*.dll' | ForEach-Object {
    try {
        $asm = [Reflection.Assembly]::LoadFrom($_.FullName)
        $hits = $asm.GetTypes() | Where-Object { $_.Name -eq 'BeatAvatarVisualController' -or $_.Name -eq 'MultiplayerGameAvatarVisualController' -or $_.Name -like '*GameAvatar*Visual*' }
        foreach ($h in $hits) { Write-Output "$($_.Name) -> $($h.FullName)" }
    } catch {}
}

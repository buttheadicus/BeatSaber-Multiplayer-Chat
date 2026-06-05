$dll = "F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Plugins\CustomAvatar.dll"
$b = [IO.File]::ReadAllBytes($dll)
$sb = New-Object System.Text.StringBuilder
$found = [System.Collections.Generic.HashSet[string]]::new()
foreach ($byte in $b) {
    if ($byte -ge 32 -and $byte -le 126) { [void]$sb.Append([char]$byte) }
    else {
        if ($sb.Length -ge 6) {
            $s = $sb.ToString()
            if ($s -match 'OnMeasureHeight|MeasureHeight|playerEyeHeight|SettingsManager|GetEyeHeight|RoomSetup|PlayerHeightDetector|automaticPlayerHeight') { [void]$found.Add($s) }
        }
        $sb.Clear() | Out-Null
    }
}
$found | Sort-Object

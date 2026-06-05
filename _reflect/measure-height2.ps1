$dll = "F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Plugins\CustomAvatar.dll"
$b = [IO.File]::ReadAllBytes($dll)
$sb = New-Object System.Text.StringBuilder
foreach ($byte in $b) {
    if ($byte -ge 32 -and $byte -le 126) { [void]$sb.Append([char]$byte) }
    else {
        if ($sb.Length -ge 8) {
            $s = $sb.ToString()
            if ($s -match 'MeasureHeight|OnMeasure|playerHeight|PlayerHeight|RoomSetup|GetHead|eyeHeight|HMD|SettingsManager|PlayerSettings') { $s }
        }
        $sb.Clear() | Out-Null
    }
}

$main = "F:\bsmanager\BSManager\BSInstances\1.40.8 (test)\Beat Saber_Data\Managed\Main.dll"
$b = [IO.File]::ReadAllBytes($main)
$sb = New-Object System.Text.StringBuilder
$found = [System.Collections.Generic.HashSet[string]]::new()
foreach ($byte in $b) {
    if ($byte -ge 32 -and $byte -le 126) { [void]$sb.Append([char]$byte) }
    else {
        if ($sb.Length -ge 8) {
            $s = $sb.ToString()
            if ($s -match 'PlayerDataModel|playerData|roomSetup|RoomSetup|playerHeight|standardHeight') { [void]$found.Add($s) }
        }
        $sb.Clear() | Out-Null
    }
}
$found | Sort-Object | Select-Object -First 40

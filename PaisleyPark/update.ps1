$path = $args[0]

$p = Get-Process PaisleyPark
Stop-Process -Id $p.id
Wait-Process -Id $p.id
Expand-Archive -path "$path\PaisleyPark.zip" -destinationpath "$path" -Force
Remove-Item -path "$path\PaisleyPark.zip"
Start-Process "$path\PaisleyPark.exe"

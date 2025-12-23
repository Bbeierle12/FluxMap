$serviceName = "NetWatchCore"
$displayName = "NetWatch Core Service"
$exePath = "C:\Users\Bbeie\NetWatch\core\src\NetWatch.CoreService\bin\Release\net8.0\NetWatch.CoreService.exe"

if (!(Test-Path $exePath)) {
  Write-Error "Service executable not found at $exePath. Build release first."
  exit 1
}

sc.exe create $serviceName binPath= "`"$exePath`"" start= auto DisplayName= "`"$displayName`""
sc.exe start $serviceName

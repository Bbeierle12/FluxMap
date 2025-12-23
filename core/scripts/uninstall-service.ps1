$serviceName = "NetWatchCore"

sc.exe stop $serviceName
sc.exe delete $serviceName

$root = "C:\Users\Bbeie\NetWatch"
$distCore = "$root\dist\core"
$distDesktop = "$root\dist\desktop"
$installer = "$root\installer"
$coreWxs = "$installer\core.wxs"
$desktopWxs = "$installer\desktop.wxs"
$coreXsl = "$installer\prefix-core.xslt"
$desktopXsl = "$installer\prefix-desktop.xslt"
$objDir = "$installer\obj"
$wixBin = "C:\Program Files (x86)\WiX Toolset v3.14\bin"
$msiOut = "$root\dist\NetWatch.msi"

if (!(Test-Path $distCore)) {
  Write-Error "Core build missing. Run core/scripts/build-release.ps1 first."
  exit 1
}
if (!(Test-Path $distDesktop)) {
  Write-Error "Desktop build missing. Run desktop/scripts/build-release.ps1 first."
  exit 1
}

if (Test-Path "$distDesktop\\web.config") { Remove-Item -Force "$distDesktop\\web.config" }

if (Test-Path $coreWxs) { Remove-Item -Force $coreWxs }
if (Test-Path $desktopWxs) { Remove-Item -Force $desktopWxs }
if (Test-Path $objDir) { Remove-Item -Recurse -Force $objDir }
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$heat = "$wixBin\heat.exe"
$candle = "$wixBin\candle.exe"
$light = "$wixBin\light.exe"

if (!(Test-Path $heat) -or !(Test-Path $candle) -or !(Test-Path $light)) {
  Write-Error "WiX v3.14 not found at $wixBin. Install WiX Toolset v3."
  exit 1
}

& $heat dir $distCore -cg CoreFiles -dr INSTALLFOLDER -gg -srd -sreg -sfrag -t $coreXsl -var var.CoreDir -out $coreWxs
& $heat dir $distDesktop -cg DesktopFiles -dr INSTALLFOLDER -gg -srd -sreg -sfrag -t $desktopXsl -var var.DesktopDir -x "$distDesktop\web.config" -out $desktopWxs

if (Test-Path $msiOut) { Remove-Item -Force $msiOut }
& $candle -dCoreDir="$distCore" -dDesktopDir="$distDesktop" -out "$objDir\\" "$installer\\NetWatch.wxs" $coreWxs $desktopWxs
& $light -sice:ICE03 -out $msiOut "$objDir\\NetWatch.wixobj" "$objDir\\core.wixobj" "$objDir\\desktop.wixobj"

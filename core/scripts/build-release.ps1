$root = "C:\Users\Bbeie\NetWatch"
$project = "$root\core\src\NetWatch.CoreService\NetWatch.CoreService.csproj"
$outDir = "$root\dist\core"

if (Test-Path $outDir) {
  Remove-Item -Recurse -Force $outDir
}
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

dotnet publish $project -c Release -o $outDir

$uiSrc = "$root\ui-web"
$uiDest = "$outDir\ui-web"
if (Test-Path $uiDest) {
  Remove-Item -Recurse -Force $uiDest
}
Copy-Item -Recurse -Force $uiSrc $uiDest

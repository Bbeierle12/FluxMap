$root = "C:\Users\Bbeie\NetWatch"
$project = "$root\desktop\NetWatch.Desktop.csproj"
$outDir = "$root\dist\desktop"

if (Test-Path $outDir) {
  Remove-Item -Recurse -Force $outDir
}
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

dotnet publish $project -c Release -p:Platform=x64 -o $outDir

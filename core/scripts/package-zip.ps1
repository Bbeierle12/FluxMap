$root = "C:\Users\Bbeie\NetWatch"
$dist = "$root\dist\core"
$zip = "$root\dist\netwatch-core.zip"

if (!(Test-Path $dist)) {
  Write-Error "Release build not found. Run build-release.ps1 first."
  exit 1
}

if (Test-Path $zip) {
  Remove-Item -Force $zip
}

Compress-Archive -Path "$dist\*" -DestinationPath $zip

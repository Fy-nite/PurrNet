param(
    [string]$AssetUrl = $args[0]
)

# example PowerShell installer for a release asset URL
$tmp = New-TemporaryFile
Remove-Item $tmp
$tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ([guid]::NewGuid().ToString()))
Invoke-WebRequest -Uri $AssetUrl -OutFile (Join-Path $tmp 'asset.zip')
Expand-Archive -Path (Join-Path $tmp 'asset.zip') -DestinationPath (Join-Path $tmp 'ex')

$main = Get-ChildItem -Path "$tmp\ex" -Recurse -File -Filter 'cmake.exe' | Select-Object -First 1
if (-not $main) {
    Write-Error "main executable not found"
    exit 1
}

$dest = $PURR_BIN_DIR
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path $main.FullName -Destination (Join-Path $dest 'cmake.exe') -Force
Write-Output "Installed cmake to $dest\cmake.exe"

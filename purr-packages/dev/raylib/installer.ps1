param(
    [string]$AssetUrl = $args[0]
)

# PowerShell version of raylib installer
$tmp = New-TemporaryFile
Remove-Item $tmp
$tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ([guid]::NewGuid().ToString()))
Invoke-WebRequest -Uri $AssetUrl -OutFile (Join-Path $tmp 'asset.zip')
Expand-Archive -Path (Join-Path $tmp 'asset.zip') -DestinationPath (Join-Path $tmp 'ex')

$main = Get-ChildItem -Path "$tmp\ex" -Recurse -File -Filter 'raylib.dll' | Select-Object -First 1
if (-not $main) {
    Write-Error "main library not found"
    exit 1
}

$dest = Join-Path $HOME '.purr\bin'
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path $main.FullName -Destination (Join-Path $dest 'raylib.dll') -Force
Write-Output "Installed raylib.dll to $dest"

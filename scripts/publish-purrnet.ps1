#!/usr/bin/env pwsh
param(
    [string]$ProjectPath = "../PurrNet/purrnet.csproj",
    [string]$Configuration = "Release",
    [string]$OutputDir = "/srv/purrnet",
    [string]$ServiceName = "purrnet"
)

function ExitIfError($code, $msg) {
    if ($code -ne 0) { Write-Error $msg; exit $code }
}

$publishTemp = Join-Path -Path (Get-Location) -ChildPath ("artifacts/publish/" + (Get-Date -Format "yyyyMMddHHmmss"))
New-Item -ItemType Directory -Force -Path $publishTemp | Out-Null

Write-Host "Restoring packages for $ProjectPath..."
dotnet restore $ProjectPath
ExitIfError $LASTEXITCODE "dotnet restore failed"

Write-Host "Publishing $ProjectPath -> $publishTemp (Configuration=$Configuration)"
dotnet publish $ProjectPath -c $Configuration -o $publishTemp
ExitIfError $LASTEXITCODE "dotnet publish failed"

Write-Host "Preparing to sync published output to $OutputDir (may prompt for sudo password)..."

# Determine whether the service is present and running so we can stop it safely
$wasActive = $false
if (Get-Command systemctl -ErrorAction SilentlyContinue) {
    & sudo systemctl is-active --quiet $ServiceName
    if ($LASTEXITCODE -eq 0) {
        $wasActive = $true
        Write-Host "Service '$ServiceName' is active — stopping it before copying."
        & sudo systemctl stop $ServiceName
        ExitIfError $LASTEXITCODE "Failed to stop $ServiceName"
    }
} else {
    Write-Warning "systemctl not available; will attempt copy without stopping service."
}

if (-not (Get-Command rsync -ErrorAction SilentlyContinue)) {
    Write-Host "rsync not found — using sudo cp fallback"
    & sudo bash -lc "mkdir -p '$OutputDir' && cp -a '$publishTemp/.' '$OutputDir/'"
    ExitIfError $LASTEXITCODE "Copy to $OutputDir failed"
} else {
    & sudo rsync -a --delete "$publishTemp/" "$OutputDir/"
    ExitIfError $LASTEXITCODE "rsync to $OutputDir failed"
}

# Copy project .env to the deployment directory if present
$projectDir = Split-Path -Path $ProjectPath -Parent
$absProjectDir = Join-Path -Path (Get-Location) -ChildPath $projectDir
$envSrc = Join-Path -Path $absProjectDir -ChildPath '.env'
if (-not (Test-Path $envSrc)) {
    # Fallback: check repository root
    $envSrc = Join-Path -Path (Get-Location) -ChildPath '.env'
}
if (Test-Path $envSrc) {
    Write-Host "Copying .env to $OutputDir"
    & sudo cp -f $envSrc "$OutputDir/.env"
    ExitIfError $LASTEXITCODE "Failed to copy .env to $OutputDir"
} else {
    Write-Host "No .env found to copy"
}

Write-Host "Published to $OutputDir"

# Start the service again only if it was running before publishing
if (Get-Command systemctl -ErrorAction SilentlyContinue) {
    if ($wasActive) {
        Write-Host "Starting systemd service '$ServiceName' (may prompt for sudo)..."
        & sudo systemctl start $ServiceName
        ExitIfError $LASTEXITCODE "Start of $ServiceName failed"
        Write-Host "Service started. Status:"
        & sudo systemctl status $ServiceName --no-pager || Write-Host "Unable to show service status"
    } else {
        Write-Host "Service '$ServiceName' was not running before publish; not starting it."
    }
} else {
    Write-Warning "systemctl not available; cannot manage service state."
}


$PurrApiUrl = 'https://purr.finite.ovh/Latest'
$RepoOwner = 'finite'
$RepoName = 'purr'

function Assert-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Error "Required command '$Name' not found. Please install it and retry."
        exit 1
    }
}

function Get-LatestVersion {
    try {
        Invoke-RestMethod -Uri $PurrApiUrl -UseBasicParsing -ErrorAction Stop
    }
    catch {
        Write-Error "Failed to fetch latest version: $_"
        return $null
    }
}

function Download-And-Install {
    param([string]$Version)
    $tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ([Guid]::NewGuid().ToString()))
    try {
        $packageFile = "$RepoName.$Version.nupkg"
        $pkgUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$Version/$packageFile"
        $pkgPath = Join-Path $tmp.FullName $packageFile
        Write-Host "Downloading $pkgUrl"
        Invoke-WebRequest -Uri $pkgUrl -OutFile $pkgPath -UseBasicParsing -ErrorAction Stop

        Write-Host "Installing global tool from nupkg..."
        dotnet tool install --global $RepoName --version $Version --add-source $tmp.FullName | Write-Host

        Write-Host "Tool installed. Run with: $RepoName (global tool)"
    }
    finally {
        Remove-Item -Recurse -Force $tmp.FullName
    }
}

function Install-Purr {
    Assert-Command dotnet
    Assert-Command Invoke-WebRequest
    $latest = Get-LatestVersion
    if (-not $latest) { return }
    $latest = $latest.Trim()
    Write-Host "Latest version: $latest"
    Download-And-Install -Version $latest
}

function Uninstall-Purr {
    Assert-Command dotnet
    Write-Host "Uninstalling global tool '$RepoName'..."
    dotnet tool uninstall --global $RepoName | Write-Host
}

function Update-Purr {
    Uninstall-Purr
    Install-Purr
}

function Show-Menu {
    Write-Host "Purr Installer"
    Write-Host "1) Install Purr"
    Write-Host "2) Uninstall Purr"
    Write-Host "3) Update Purr"
    Write-Host "4) Exit"
}

# Script entrypoint
while ($true) {
    Show-Menu
    $choice = Read-Host 'Enter choice (1-4)'
    switch ($choice) {
        '1' { Install-Purr }
        '2' { Uninstall-Purr }
        '3' { Update-Purr }
        '4' { break }
        default { Write-Host 'Invalid choice' }
    }
    Write-Host ""
}

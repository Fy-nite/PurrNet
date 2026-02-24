$PurrApiUrl = 'https://purr.finite.ovh/Latest'
$RepoOwner = 'finite'
$RepoName = 'PurrNet'
$TargetDir = Join-Path $env:USERPROFILE '.purr'

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

function Download-And-Build {
    param([string]$Version)
    $tmp = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ([Guid]::NewGuid().ToString()))
    try {
        $zipUrl = "https://github.com/$RepoOwner/$RepoName/archive/refs/tags/$Version.zip"
        $zipPath = Join-Path $tmp.FullName "$Version.zip"
        Write-Host "Downloading $zipUrl"
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing

        Write-Host "Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $tmp.FullName

        $extracted = Get-ChildItem -Path $tmp.FullName -Directory | Where-Object { $_.Name -like "$RepoName-*" } | Select-Object -First 1
        if (-not $extracted) { throw 'Extracted folder not found' }

        $csproj = Get-ChildItem -Path $extracted.FullName -Recurse -Filter *.csproj | Select-Object -First 1
        if (-not $csproj) { throw '.csproj not found' }

        Write-Host "Building $($csproj.FullName)"
        dotnet build $csproj.FullName -c Release | Write-Host

        $dll = Get-ChildItem -Path $extracted.FullName -Recurse -Filter 'purr*.dll' | Where-Object { $_.FullName -match '\\bin\\Release\\' } | Select-Object -First 1
        if (-not $dll) { throw 'Built artifact not found' }

        if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir | Out-Null }
        Copy-Item -Path $dll.FullName -Destination $TargetDir -Force
        Write-Host "Installed to $TargetDir\$(Split-Path $dll.FullName -Leaf)"
        Write-Host "Run with: dotnet $TargetDir\$(Split-Path $dll.FullName -Leaf)"
    }
    finally {
        Remove-Item -Recurse -Force $tmp.FullName
    }
}

function Install-Purr {
    Assert-Command dotnet
    Assert-Command Expand-Archive
    $latest = Get-LatestVersion
    if (-not $latest) { return }
    $latest = $latest.Trim()
    Write-Host "Latest version: $latest"
    Download-And-Build -Version $latest
}

function Uninstall-Purr {
    if (Test-Path $TargetDir) {
        Get-ChildItem -Path $TargetDir -Filter 'purr*.dll' -File | Remove-Item -Force
        Write-Host "Purr removed from $TargetDir"
    }
    else { Write-Host "Nothing to remove at $TargetDir" }
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

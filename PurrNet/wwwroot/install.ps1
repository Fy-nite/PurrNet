$PurrApiUrl = 'https://purr.finite.ovh/Latest'
$RepoOwner = 'Fy-nite'
$RepoName = 'PurrNet'
# Package id used for dotnet global tool install. Adjust if different.
$ToolId = 'purr'

function Assert-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Error "Required command '$Name' not found. Please install it and retry."
        exit 1
    }
}

function Get-LatestVersion {
    try {
        $resp = Invoke-RestMethod -Uri $PurrApiUrl -ErrorAction Stop
        return $resp
    }
    catch {
        Write-Error "Failed to fetch latest version: $_"
        return $null
    }
}

function Download-And-Install {
    param([string]$Version)

    # Determine a safe temp directory base
    $baseTemp = $env:TEMP; if (-not $baseTemp) { $baseTemp = $env:TMP }
    if (-not $baseTemp) { $baseTemp = [IO.Path]::GetTempPath() }

    $tmpDir = Join-Path $baseTemp ([Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Path $tmpDir | Out-Null

    try {
        $pkgFile1 = "$RepoName.$Version.nupkg"
        $pkgFile2 = "purr.$Version.nupkg"
        $pkgFile3 = "$RepoName.v$Version.nupkg"
        $pkgPath = $null
        $tried = @()

        # Helper: download using available tool
        function Download-File($url, $outPath) {
            if (Get-Command Invoke-WebRequest -ErrorAction SilentlyContinue) {
                Invoke-WebRequest -Uri $url -OutFile $outPath -ErrorAction Stop; return $true
            }
            elseif (Get-Command curl -ErrorAction SilentlyContinue) {
                & curl -L -o $outPath $url 2>$null; return $LASTEXITCODE -eq 0
            }
            elseif (Get-Command wget -ErrorAction SilentlyContinue) {
                & wget -q -O $outPath $url; return $LASTEXITCODE -eq 0
            }
            else { throw "No HTTP downloader available (Invoke-WebRequest, curl or wget)." }
        }

        foreach ($pkg in @($pkgFile1, $pkgFile2, $pkgFile3)) {
            $pkgUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/v$Version/$pkg"
            $out = Join-Path $tmpDir $pkg
            $tried += $pkg
            Write-Host "Attempting to download $pkgUrl ..."
            try {
                if (Download-File $pkgUrl $out) { $pkgPath = $out; break }
            }
            catch { }
        }

        if (-not $pkgPath -or -not (Test-Path $pkgPath)) {
            Write-Error "Failed to download any nupkg (tried: $($tried -join ' '))"
            return 1
        }

        Write-Host "Installing global tool from local nupkg source..."
        $installArgs = @('tool','install','--global',$ToolId,'--version',$Version,'--add-source',$tmpDir)
        $proc = Start-Process -FilePath dotnet -ArgumentList $installArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput ([IO.Path]::Combine($tmpDir,'dotnet-out.txt')) -RedirectStandardError ([IO.Path]::Combine($tmpDir,'dotnet-err.txt'))
        if ($proc.ExitCode -ne 0) {
            Write-Error "dotnet tool install failed. See logs in $tmpDir"
            Get-Content (Join-Path $tmpDir 'dotnet-err.txt') -ErrorAction SilentlyContinue | Write-Host
            return 1
        }

        Write-Host "Tool installed (global). You can run: $ToolId"
        return 0
    }
    finally {
        if (Test-Path $tmpDir) {
            try { Remove-Item -Recurse -Force $tmpDir } catch { }
        }
    }
}

function Install-Purr {
    Assert-Command dotnet
    $latest = Get-LatestVersion
    if (-not $latest) { return }
    $latest = $latest.ToString().Trim()
    Write-Host "Latest version: $latest"
    Download-And-Install -Version $latest | Out-Null
}

function Uninstall-Purr {
    Assert-Command dotnet
    Write-Host "Uninstalling global tool '$ToolId'..."
    dotnet tool uninstall --global $ToolId | Write-Host
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

# If script is piped in (non-interactive), perform install directly
if ($MyInvocation.ExpectingInput) {
    Install-Purr
    exit 0
}

# Interactive menu
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

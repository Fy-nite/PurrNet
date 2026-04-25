# purr — Finite User Repository CLI

`purr` is a lightweight, cross-platform package manager that installs and manages software packages registered in a PurrNet repository. It resolves metadata from the registry API, downloads release assets or clones git repositories, runs installer scripts, and wires up binaries on your `PATH`.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Build from Source](#build-from-source)
3. [How Package Installation Works](#how-package-installation-works)
4. [Directory Layout](#directory-layout)
5. [Commands Reference](#commands-reference)
   - [install](#install)
   - [uninstall](#uninstall)
   - [update](#update)
   - [upgrade](#upgrade)
   - [downgrade](#downgrade)
   - [versions](#versions)
   - [search](#search)
   - [list](#list)
   - [info](#info)
   - [stats](#stats)
6. [Configuration: fursettings.json](#configuration-fursettingsjson)
7. [Package Manifest: furconfig.json](#package-manifest-furconfigjson)
8. [Installer Scripts](#installer-scripts)
9. [PATH Setup](#path-setup)
10. [API Integration](#api-integration)
11. [Development](#development)

---

## Prerequisites

| Requirement | Notes |
|---|---|
| .NET 8.0 Runtime | Required to run the CLI |
| Git | Required for packages that use the clone-based install path |

---

## Build from Source

```bash
git clone https://github.com/Fy-nite/PurrNet
cd PurrNet/purr
dotnet build --configuration Release
```

The output binary lands in `bin/Release/net8.0/`.

To run directly without installing:
```bash
dotnet run -- <command> [arguments]
```

---

## How Package Installation Works

When you run `purr install <package>`, the following steps happen:

1. **Fetch metadata** — `purr` queries each configured repository API (in order) at  
   `GET /api/v1/packages/<name>` (or `/<name>/<version>` for a pinned version) until it gets a `200 OK`.

2. **Resolve dependencies** — Any packages listed in the `dependencies` field of the manifest are installed recursively first, in the same way.

3. **Download & install** — The install strategy depends on whether the manifest has an `installer` field:

   **A. Release asset install (no `installer` field)**  
   - Queries the GitHub Releases API for the package's git repository.  
   - Selects the most appropriate asset for the current OS (matching `windows`, `linux`, `mac`/`darwin` in the filename; falls back to the first asset).  
   - Downloads the asset to a temporary directory.  
   - If the asset is a `.zip`, it is extracted and the first executable found inside is used.  
   - The binary is copied to `<purr_folder>/bin/<package-name>` and marked executable.  
   - `purr` then checks whether or not the bin folder is on your `PATH` and prints instructions if it is not (see [PATH Setup](#path-setup)).

   **B. Script-based install (has `installer` field)**  
   - If a local clone already exists at `<purr_folder>/packages/<name>/.git`, `purr` fetches and checks out the requested version.  
   - If no clone exists, `purr` runs `git clone <git-url> <purr_folder>/packages/<name>` and checks out the requested version.  
   - The installer script (e.g. `install.sh`) is located at the root of the cloned repository and is executed.  
   - A `furconfig.json` metadata snapshot is written alongside the clone.

4. **Track download** — A `POST /api/v1/packages/<name>/download` request is sent to the registry to increment the download counter.

---

## Directory Layout
purr_folder is `$XDG_DATA_HOME/purr` (Linux), `~/.purr` (MacOS) or `%LOCALAPPDATA%\purr` (Windows)

```
<purr_folder>
└── bin/                      # Binaries installed from release assets
    └── <package-name>        # Executable (or .exe on Windows)

<purr_folder>
└── packages/
    └── <package-name>/       # One directory per script-installed package
        ├── .git/             # Full git clone of the package's repository
        ├── furconfig.json    # Metadata snapshot written at install time
        └── <installer>       # e.g. install.sh / uninstall.sh
```

The `purr` CLI itself reads its own configuration from:
```
<purr-executable-directory>/
└── fursettings.json          # Repository URLs (see Configuration section)
```

---

## Commands Reference

### install

Install a package, optionally pinning to a version.

```
purr install <package>[@<version>]
```

| Argument | Description |
|---|---|
| `package` | Package name, optionally suffixed with `@<version>` |

**Examples:**
```bash
purr install neofetch
purr install neofetch@2.0.0
```

---

### uninstall

Remove an installed (script-based) package. If the package ships an uninstall script (`uninstall.sh` / equivalent), it is run first; then the clone directory is deleted.

```
purr uninstall <package>
```

| Argument | Description |
|---|---|
| `package` | Name of the installed package |

**Example:**
```bash
purr uninstall neofetch
```

> **Note:** Release-asset-installed binaries are not currently removed by `uninstall`. Delete them manually if needed.

---

### update

Pull the latest changes for an already-installed package (equivalent to re-running install).

```
purr update <package>
```

| Argument | Description |
|---|---|
| `package` | Package name, optionally suffixed with `@<version>` |

---

### upgrade

Upgrade a package to its latest version, or to a specific version. Functionally identical to `install` — it re-runs the full fetch-and-install flow.

```
purr upgrade <package>[@<version>]
```

---

### downgrade

Install an older specific version of a package. A version is required.

```
purr downgrade <package>@<version>
```

**Example:**
```bash
purr downgrade neofetch@1.8.0
```

Use `purr versions <package>` first to see what versions are available.

---

### versions

List all versions of a package that the registry knows about. The latest version is highlighted.

```
purr versions <package>
```

**Example:**
```bash
purr versions neofetch
```

---

### search

Search for packages by name or description across all configured repositories.

```
purr search <query>
```

| Argument | Description |
|---|---|
| `query` | Free-text search string |

**Example:**
```bash
purr search "system info"
```

---

### list

List all available packages. Optionally filter by category or change the sort order.

```
purr list [--sort <method>] [--category <name>]
```

| Option | Description |
|---|---|
| `--sort <method>` | Sort order (see table below) |
| `--category <name>` | Filter to packages in a specific category |

**Sort methods:**

| Value | Description |
|---|---|
| `name` | Alphabetical (default) |
| `mostDownloads` | Most downloaded |
| `leastDownloads` | Least downloaded |
| `recentlyUpdated` | Most recently updated |
| `recentlyUploaded` | Most recently uploaded |
| `oldestUpdated` | Longest since last update |
| `oldestUploaded` | First ever uploaded |

**Examples:**
```bash
purr list
purr list --sort mostDownloads
purr list --category utilities
```

---

### info

Show detailed metadata for a package.

```
purr info <package> [--version <version>]
```

| Argument / Option | Description |
|---|---|
| `package` | Package name |
| `--version <version>` | Show info for a specific version instead of latest |

**Examples:**
```bash
purr info neofetch
purr info neofetch --version 2.0.0
```

---

### stats

Display aggregate statistics for the registry (total packages, downloads, views, most downloaded, recently added).

```
purr stats
```

---

## Configuration: fursettings.json

`purr` reads `fursettings.json` from the same directory as its own executable. Use this file to point at private or self-hosted PurrNet instances, or to add fallback mirrors.

```json
{
  "repositories": [
    "http://purr.finite.ovh",
    "http://my-internal-registry:5001"
  ]
}
```

Repositories are queried **in order**; the first one that returns a successful response for a given package is used. If the file is absent, `purr` defaults to `http://purr.finite.ovh`.

---

## Package Manifest: furconfig.json

Every package registered in the registry has a `furconfig.json` file that describes it. You can inspect a package's manifest with `purr info <package>`.

```json
{
  "name": "my-package",
  "version": "1.0.0",
  "description": "A sample package",
  "authors": ["Jane Doe"],
  "homepage": "https://example.com",
  "issue_tracker": "https://github.com/user/repo/issues",
  "git": "https://github.com/user/repo.git",
  "installer": "install.sh",
  "dependencies": ["dep1", "dep2"],
  "categories": ["utilities"]
}
```

| Field | Required | Description |
|---|---|---|
| `name` | ✅ | Unique package identifier |
| `version` | ✅ | Semantic version string or `latest` |
| `description` | | Short description shown in search results |
| `authors` | | Array of author names |
| `homepage` | | Project homepage URL |
| `issue_tracker` | | URL for filing bug reports |
| `git` | ✅ | Git repository URL (HTTPS) |
| `installer` | | Filename of the installer script at the repo root |
| `dependencies` | | Array of other package names to install first |
| `categories` | | Array of category tags |

When `installer` is **omitted**, `purr` attempts to download a matching binary from the repository's GitHub Releases. When `installer` is **present**, `purr` clones the repository and runs that script.

---

## Installer Scripts

For script-based packages, `purr` detects the script type from its file extension and runs it with the appropriate interpreter:

| Extension | Interpreter |
|---|---|
| `.sh` | `bash` |
| `.ps1` | `pwsh -ExecutionPolicy Bypass -File` |
| `.py` | `python` |
| `.js` | `node` |
| `.rb` | `ruby` |
| `.cmd` / `.bat` | `cmd /c` (Windows only) |
| `.exe` | Direct execution (Windows only) |
| *(none)* | Reads shebang line; falls back to `bash` on Unix |

### Environment Variables for Installer Scripts

When running installer (and uninstaller) scripts, `purr` sets several environment variables that provide useful context to your script:

| Variable              | Description                                               |
|-----------------------|-----------------------------------------------------------|
| `PURR_CWD`            | The directory where `purr` was invoked from               |
| `PURR_INSTALL_DIR`    | The directory where the package is being installed (the script's directory) |
| `PURR_PACKAGE_NAME`   | The name of the package being installed                   |
| `PURR_BIN_DIR`        | The directory to place package binaries                   |

These variables are available in all script types:

- **PowerShell:** `$env:PURR_INSTALL_DIR`, `$env:PURR_CWD`, `$env:PURR_PACKAGE_NAME`
- **Bash/sh:** `$PURR_INSTALL_DIR`, `$PURR_CWD`, `$PURR_PACKAGE_NAME`

Use these variables to reference install locations, perform file operations, or customize install logic based on context.

Installer output is streamed live to the terminal. If the script exits with a non-zero code, the installation is aborted.

For **uninstall**, `purr` looks for a script with the same name but `install` replaced by `uninstall` (e.g. `install.sh` → `uninstall.sh`). If that file does not exist, only the package directory is removed.

---

## PATH Setup

Release-asset binaries are placed in `<purr_folder>/bin` `purr` checks after every install whether this directory is already on `PATH` and prints the appropriate shell commands if it is not.

**Bash / Zsh (one-time, current session):**
```bash
export PATH="${XDG_DATA_HOME:-~/.local/share}/purr/bin:$PATH"
```

**Persist in Bash (`~/.bashrc`) or Zsh (`~/.zshrc`):**
```bash
echo 'export PATH="$PATH:${XDG_DATA_HOME:-~/.local/share}/purr/bin"' >> ~/.bashrc
source ~/.bashrc
```

**Fish (persistent):**
```fish
set -U fish_user_paths $HOME/.local/share/purr/bin $fish_user_paths
```

**PowerShell (current session):**
```powershell
$env:Path = "$env:LOCALAPPDATA\purr\bin;$env:Path"
```

**PowerShell / CMD (persist via `setx`):**
```cmd
setx PATH "%LOCALAPPDATA%\purr\bin;%PATH%"
```

---

## API Integration

`purr` communicates with a PurrNet REST API. All endpoints are relative to the base URL(s) in `fursettings.json`.

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/packages` | List packages (supports `sort`, `search`, `page`, `pageSize`, `details`) |
| `GET` | `/api/v1/packages/<name>` | Latest manifest for a package |
| `GET` | `/api/v1/packages/<name>/<version>` | Manifest for a specific version |
| `POST` | `/api/v1/packages/<name>/download` | Increment download counter |
| `GET` | `/api/v1/packages/statistics` | Registry-wide statistics |
| `GET` | `/api/v1/packages/tags` | Popular tags |
| `GET` | `/api/v1/packages/tags/<tag>` | Packages by tag |
| `GET` | `/api/v1/packages/authors` | Popular authors |
| `GET` | `/health` | API health check |

See [api.md](api.md) for full request/response schemas.

---

## Development

### Project Structure

```
purr/
├── Program.cs               # CLI entry point — command definitions and wiring
├── fursettings.json         # Default/example repository config
├── purr.csproj
├── Services/
│   ├── PackageManager.cs    # Core install, uninstall, update, search logic
│   └── ApiService.cs        # HTTP client wrapper for the registry API
├── Models/
│   ├── FurConfig.cs         # Package manifest model (furconfig.json)
│   ├── FurSettings.cs       # CLI settings model (fursettings.json)
│   ├── PackageListResponse.cs
│   ├── PackageSearchResult.cs
│   ├── HealthStatus.cs
│   └── RepositoryStatistics.cs
└── Utils/
    └── ConsoleHelper.cs     # Coloured terminal output helpers
```

### Building

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Run without installing
dotnet run -- install neofetch
dotnet run -- search "system info"
dotnet run -- list --sort mostDownloads
dotnet run -- info neofetch
dotnet run -- stats
```

---

## License

AGPL-3.0

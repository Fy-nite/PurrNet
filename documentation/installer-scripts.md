# Installer Scripts & PATH Setup

Installer script behavior:

- `purr` detects script type by extension and runs with the appropriate interpreter (e.g. `.sh` → `bash`, `.ps1` → `pwsh -File`). If no extension, `purr` reads the shebang or falls back to `bash` on Unix.
- Output from installer scripts is streamed to the terminal; a non-zero exit code aborts the installation.
- For uninstall, `purr` looks for an `uninstall` variant of the installer (e.g. `install.sh` → `uninstall.sh`) and runs it if present, then removes the package clone.

PATH setup for release-asset-installed binaries:

- Unix (Bash/Zsh): `~/.purr/bin` — add with:

```bash
export PATH="$HOME/.purr/bin:$PATH"
```

- Persist in `~/.bashrc` / `~/.zshrc` by echoing the export line and sourcing the file.
- Fish: use `set -U fish_user_paths $HOME/.purr/bin $fish_user_paths`.
- PowerShell: add `%USERPROFILE%\.purr\bin` to `PATH` for session or persist via `setx`.

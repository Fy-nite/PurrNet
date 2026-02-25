# Purr Packages Collection

This repository holds a set of *reference* packages that the `purr` CLI can install via
installer scripts or GitHub release assets.  It is primarily intended for
maintainers and newcomers who want a starting point for packaging software.

Each subdirectory under `dev/` represents a single package.  It should contain:

- `furconfig.json` – metadata used by `purr` (name, version, git URL, installer, etc.)
- `installer.sh` / `installer.ps1` – cross‑platform installer scripts that the CLI
  will execute when the package is requested.
- Any supporting files (e.g. `uninstall.*`, examples, etc.).

You can publish this whole repository as an organisation‑owned repo, or simply
point users to individual package directories if you choose to split things up.

## Adding a package

1. Create a new subfolder under `dev/` with the package name.
2. Add a `furconfig.json` with the required fields (`name`, `version`, `git`).
3. Add one or more installer scripts.  See the existing `cmake` and `raylib`
   folders for templates.
4. (Optional) publish a GitHub release with platform‑specific assets.  Leave
   `installer` out of `furconfig.json` and `purr` will automatically attempt a
   release‑asset install.

Happy packaging!  🐾
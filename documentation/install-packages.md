# Installing Packages with `purr`

This page describes how `purr` installs packages from a PurrNet registry.

1. Fetch metadata

   `purr` queries each configured repository API in order at `GET /api/v1/packages/<name>` (or `/<name>/<version>` for a pinned version) until it receives a `200 OK` response.

2. Resolve dependencies

   Any packages listed in the manifest's `dependencies` array are installed recursively before the requested package.

3. Download & install

   - Release asset install (no `installer` field):
     - `purr` queries the repository's GitHub Releases for the package repository, selects the best-matching asset for the OS, downloads it, extracts if necessary, and copies the executable to the CLI binary directory (`~/.purr/bin` on Unix or `%USERPROFILE%\.purr\bin` on Windows).

   - Script-based install (has `installer` field):
     - `purr` clones (or updates) the package repository into `~/.purr/packages/<name>`, checks out the requested version, and runs the installer script (for example `install.sh`). A `furconfig.json` snapshot is written next to the clone.

4. Track download

   After a successful install, `purr` posts `POST /api/v1/packages/<name>/download` to increment the registry download counter.

Examples:

```
purr install neofetch
purr install neofetch@2.0.0
```

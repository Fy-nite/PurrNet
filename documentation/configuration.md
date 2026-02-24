# Configuration & Package Manifests

`purr` reads `fursettings.json` from the same directory as the `purr` executable. This file lists repository base URLs used to fetch package metadata.

Example `fursettings.json`:

```json
{
  "repositories": [
    "http://purr.finite.ovh",
    "http://my-internal-registry:5001"
  ]
}
```

Package manifest (`furconfig.json`) fields:

- `name` (required): unique package identifier
- `version` (required): semantic version or `latest`
- `git` (required for script installs): HTTPS git repo URL
- `installer`: installer filename at repo root (e.g. `install.sh`)
- `dependencies`: array of package names to install first
- `categories`, `description`, `authors`, `homepage`, `issue_tracker` — optional metadata fields

When `installer` is omitted, `purr` attempts a release-asset install via GitHub Releases for that package repository.

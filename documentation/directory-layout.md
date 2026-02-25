# Directory Layout

Where `purr` and script-installed packages keep their files.

User-level directories:

```
~/.purr/
└── bin/                      # Binaries installed from release assets
    └── <package-name>

~/.purr/
└── packages/
    └── <package-name>/       # One directory per script-installed package
        ├── .git/             # Full git clone of the package's repository
        ├── furconfig.json    # Metadata snapshot written at install time
        └── <installer>       # e.g. install.sh / uninstall.sh
```

Notes:
- Release-asset-installed executables are placed in `~/.purr/bin` (or `%USERPROFILE%\.purr\bin` on Windows).
- Script-based packages are full git clones under `~/.purr/packages` so installers can run from the repo.
- `purr` does not automatically remove binaries from `~/.purr/bin` when uninstalling script-based packages; remove them manually if needed.

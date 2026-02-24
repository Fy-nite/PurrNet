# Commands Reference (summary)

Common `purr` commands and quick usage:

- `purr install <package>[@<version>]` — Install or upgrade a package.
- `purr uninstall <package>` — Remove a script-installed package (runs uninstall script if present).
- `purr update <package>` — Pull latest changes for an installed script-based package.
- `purr upgrade <package>[@<version>]` — Upgrade a package (aliases to install flow).
- `purr downgrade <package>@<version>` — Install an older version (version required).
- `purr versions <package>` — List available versions for a package.
- `purr search <query>` — Search package names and descriptions across repositories.
- `purr list [--sort <method>] [--category <name>]` — List packages with optional sorting and filtering.
- `purr info <package> [--version <version>]` — Show package metadata.
- `purr stats` — Show registry statistics.

See the `purr` CLI help for full details and available flags.

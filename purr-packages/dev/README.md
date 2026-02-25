# Development packages

This directory contains "dev" or example packages that are maintained by the
PurrNet project itself.  They demonstrate the basic structure and installer
logic.  Feel free to copy them when creating your own packages.

- `cmake/` – simple script that downloads a GitHub release asset and installs
  the `cmake` binary.
- `raylib/` – similar example for the raylib graphics library.

Each package can be consumed by `purr` either via the git URL in `furconfig.json`
or by configuring a repository entry in `fursettings.json` for this directory
(if you serve it over HTTP/HTTPS).
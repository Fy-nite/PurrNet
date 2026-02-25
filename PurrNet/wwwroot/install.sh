#!/usr/bin/env bash
set -euo pipefail

PURR_API_URL="https://purr.finite.ovh/Latest"
REPO_OWNER="fy-nite"
# package / tool id (adjust if your published package uses a different id)
REPO_NAME="PurrNet"
TARGET_DIR="$HOME/.purr"

check_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command '$1' not found. Please install it and retry." >&2; exit 1; }
}

get_latest_version() {
  if command -v curl >/dev/null 2>&1; then
    curl -fsS "$PURR_API_URL" || { echo "Failed to fetch latest version." >&2; return 1; }
  elif command -v wget >/dev/null 2>&1; then
    wget -qO- "$PURR_API_URL" || { echo "Failed to fetch latest version." >&2; return 1; }
  else
    echo "curl or wget required to fetch latest version." >&2
    return 1
  fi
}

download_and_install() {
  local version="$1"
  local tmpdir
  tmpdir=$(mktemp -d)
  trap 'rm -rf "$tmpdir"' EXIT

  # Try common nupkg asset name patterns
  local pkgfile1="${REPO_NAME}.${version}.nupkg"
  local pkgfile2="purr.${version}.nupkg"
  local pkgfile3="${REPO_NAME}.v${version}.nupkg"
  local tried=""
  local pkgurl
  local pkgpath
  # https://github.com/Fy-nite/PurrNet/releases/download/v1.0.0/purr.1.0.0.nupkg
  for pkg in "$pkgfile1" "$pkgfile2" "$pkgfile3"; do
    pkgurl="https://github.com/${REPO_OWNER}/${REPO_NAME}/releases/download/v${version}/${pkg}"
    pkgpath="$tmpdir/$pkg"
    echo "Attempting to download ${pkgurl} ..."
    if command -v curl >/dev/null 2>&1; then
      if curl -L -f -o "$pkgpath" "$pkgurl" 2>/dev/null; then
        echo "Downloaded $pkg"
        break
      fi
    else
      if wget -q -O "$pkgpath" "$pkgurl" 2>/dev/null; then
        echo "Downloaded $pkg"
        break
      fi
    fi
    tried+="$pkg "
  done

  if [[ ! -f "$pkgpath" ]]; then
    echo "Failed to download any nupkg (tried: $tried)" >&2
    return 1
  fi

  echo "Installing global tool from local nupkg source..."
  # Use the temporary directory as a local package source
  if ! dotnet tool install --global purr --version "$version" --add-source "$tmpdir" ; then
    echo "dotnet tool install failed. Ensure the package id/version match and that you're allowed to install global tools." >&2
    return 1
  fi

  echo "Tool installed (global). You can run: purr"
}

install() {
  check_cmd dotnet
  if ! command -v curl >/dev/null 2>&1 && ! command -v wget >/dev/null 2>&1; then
    echo "curl or wget required to download releases." >&2
    exit 1
  fi

  local latest
  latest=$(get_latest_version) || exit 1
  latest=$(echo "$latest" | tr -d '\r\n' )
  echo "Latest version: $latest"
  download_and_install "$latest"
}

uninstall() {
  check_cmd dotnet
  echo "Uninstalling global tool 'purr'..."
  if dotnet tool uninstall --global "purr"; then
    echo "Uninstalled global tool: purr"
  else
    echo "Failed to uninstall or tool not present."
  fi
}

update() {
  uninstall
  install
}

print_menu() {
  cat <<EOF
Purr Installer
1) Install Purr
2) Uninstall Purr
3) Update Purr
4) Exit
EOF
}

# when the script is piped into bash (`curl ... | bash`) BASH_SOURCE
# may be undefined, and `set -u` would error.  use parameter expansion with
# a default value so the test still works.
if [[ "${BASH_SOURCE[0]:-}" == "$0" ]]; then
  # if stdin isn't a terminal then we're running non‑interactively (e.g. a
  # pipe).  skip the menu and perform the default install path directly.
  if ! [ -t 0 ]; then
    install
    exit 0
  fi

  while true; do
    print_menu
    read -rp $'Enter choice (1-4): ' choice
    case "$choice" in
      1) install ;;
      2) uninstall ;;
      3) update ;;
      4) exit 0 ;;
      *) echo "Invalid choice" ;;
    esac
    echo
  done
fi

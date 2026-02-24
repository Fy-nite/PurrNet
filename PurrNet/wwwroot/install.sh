PURR_API_URL="https://purr.finite.ovh/Latest"
REPO_OWNER="finite"
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

download_and_build() {
  local version="$1"
  local tmpdir
  tmpdir=$(mktemp -d)
  trap 'rm -rf "$tmpdir"' EXIT

  local zipurl="https://github.com/${REPO_OWNER}/${REPO_NAME}/archive/refs/tags/purr-${version}.zip"
  local zipfile="$tmpdir/${version}.zip"

  echo "Downloading ${zipurl} ..."
  if command -v curl >/dev/null 2>&1; then
    curl -L -f -o "$zipfile" "$zipurl"
  else
    wget -O "$zipfile" "$zipurl"
  fi

  echo "Extracting..."
  unzip -q "$zipfile" -d "$tmpdir"

  local extracted
  extracted=$(find "$tmpdir" -maxdepth 1 -type d -name "*${REPO_NAME}-*" | head -n1)
  if [[ -z "$extracted" ]]; then
    echo "Could not find extracted source folder." >&2
    return 1
  fi

  local csproj
  csproj=$(find "$extracted" -type f -name "*.csproj" | head -n1)
  if [[ -z "$csproj" ]]; then
    echo "No .csproj found in source." >&2
    return 1
  fi

  echo "Building project: $csproj"
  dotnet build "$csproj" -c Release

  local dllpath
  dllpath=$(find "$extracted" -type f -name "purr*.dll" -path "*/bin/Release/*" | head -n1)
  if [[ -z "$dllpath" ]]; then
    echo "Built artifact not found." >&2
    return 1
  fi

  mkdir -p "$TARGET_DIR"
  cp -f "$dllpath" "$TARGET_DIR/"
  echo "Installed to $TARGET_DIR/$(basename "$dllpath")"
  echo "Add $TARGET_DIR to your PATH or run with 'dotnet $TARGET_DIR/$(basename "$dllpath")'"
}

install() {
  check_cmd dotnet
  check_cmd unzip
  if ! command -v curl >/dev/null 2>&1 && ! command -v wget >/dev/null 2>&1; then
    echo "curl or wget required to download releases." >&2
    exit 1
  fi

  local latest
  latest=$(get_latest_version) || exit 1
  latest=$(echo "$latest" | tr -d '\r\n' )
  echo "Latest version: $latest"
  download_and_build "$latest"
}

uninstall() {
  if [[ -d "$TARGET_DIR" ]]; then
    echo "Removing purr artifacts from $TARGET_DIR"
    rm -f "$TARGET_DIR"/purr*.dll || true
    echo "Uninstalled.";
  else
    echo "Nothing to remove at $TARGET_DIR"
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

if [[ ${BASH_SOURCE[0]} == "$0" ]]; then
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

#!/usr/bin/env bash
# Deskboard Native — one-line installer for Linux and macOS.
#
#   curl -fsSL https://raw.githubusercontent.com/Wrecht-Dev/deskboard-native/main/scripts/install.sh | bash
#
# Environment overrides:
#   DESKBOARD_VERSION       release tag to install (default: latest)
#   DESKBOARD_INSTALL_DIR   install directory (default: ~/.local/share/deskboard-native)
#   DESKBOARD_BIN_DIR       launcher directory (default: ~/.local/bin)
#   DESKBOARD_LOCAL_TARBALL install from a local .tar.gz (testing / offline)
set -euo pipefail

REPO="Wrecht-Dev/deskboard-native"
INSTALL_DIR="${DESKBOARD_INSTALL_DIR:-$HOME/.local/share/deskboard-native}"
BIN_DIR="${DESKBOARD_BIN_DIR:-$HOME/.local/bin}"
VERSION="${DESKBOARD_VERSION:-latest}"

say()  { printf '  %s\n' "$1"; }
ok()   { printf '\033[32m✓\033[0m %s\n' "$1"; }
die()  { printf '\033[31m✗\033[0m %s\n' "$1" >&2; exit 1; }

case "$(uname -s)" in
  Linux)  OS="linux" ;;
  Darwin) OS="macos" ;;
  *) die "Unsupported OS: $(uname -s). On Windows use scripts/install.ps1." ;;
esac

case "$(uname -m)" in
  x86_64|amd64) ARCH="x64" ;;
  arm64|aarch64) ARCH="arm64" ;;
  *) die "Unsupported architecture: $(uname -m)" ;;
esac

[ "$OS" = "linux" ] && [ "$ARCH" = "arm64" ] && die "No Linux arm64 build yet."

ASSET="deskboard-native-$OS-$ARCH.tar.gz"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [ -n "${DESKBOARD_LOCAL_TARBALL:-}" ]; then
  say "Using local archive: $DESKBOARD_LOCAL_TARBALL"
  cp "$DESKBOARD_LOCAL_TARBALL" "$TMP/$ASSET"
else
  if [ "$VERSION" = "latest" ]; then
    URL="https://github.com/$REPO/releases/latest/download/$ASSET"
  else
    URL="https://github.com/$REPO/releases/download/$VERSION/$ASSET"
  fi
  say "Downloading $ASSET…"
  curl -fL --retry 3 --progress-bar "$URL" -o "$TMP/$ASSET" || die "Download failed: $URL"
fi

say "Installing to $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
tar -xzf "$TMP/$ASSET" -C "$TMP"
SRC="$TMP/deskboard-native-$OS-$ARCH"
[ -d "$SRC" ] || SRC="$(find "$TMP" -maxdepth 2 -type d -name 'deskboard-native-*' | head -1)"
[ -n "$SRC" ] && [ -d "$SRC" ] || die "Archive has an unexpected layout."

rm -rf "$INSTALL_DIR"
mkdir -p "$(dirname "$INSTALL_DIR")"
cp -r "$SRC" "$INSTALL_DIR"

if [ "$OS" = "macos" ] && [ -d "$INSTALL_DIR/Deskboard.app" ]; then
  APP_DIR="$HOME/Applications"
  mkdir -p "$APP_DIR"
  rm -rf "$APP_DIR/Deskboard.app"
  cp -r "$INSTALL_DIR/Deskboard.app" "$APP_DIR/Deskboard.app"
  ok "Installed app: $APP_DIR/Deskboard.app"
fi

mkdir -p "$BIN_DIR"
LAUNCH="$BIN_DIR/deskboard-native"
if [ "$OS" = "macos" ] && [ -x "$INSTALL_DIR/Deskboard.app/Contents/MacOS/Deskboard.App" ]; then
  printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$INSTALL_DIR/Deskboard.app/Contents/MacOS/Deskboard.App" > "$LAUNCH"
else
  printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$INSTALL_DIR/Deskboard.App" > "$LAUNCH"
fi
chmod +x "$LAUNCH" "$INSTALL_DIR"/* 2>/dev/null || true

if [ "$OS" = "linux" ]; then
  APPS="$HOME/.local/share/applications"
  ICONS="$HOME/.local/share/icons/hicolor/256x256/apps"
  mkdir -p "$APPS" "$ICONS"
  sed "s|^Exec=.*|Exec=$LAUNCH|" "$INSTALL_DIR/deskboard-native.desktop" > "$APPS/deskboard-native.desktop"
  cp "$INSTALL_DIR/icon.png" "$ICONS/deskboard-native.png"
  command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS" 2>/dev/null || true
  command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
fi

ok "Deskboard Native installed."
case ":$PATH:" in
  *":$BIN_DIR:"*) say "Run: deskboard-native" ;;
  *) say "Add $BIN_DIR to your PATH, then run: deskboard-native"
     say "  export PATH=\"$BIN_DIR:\$PATH\"" ;;
esac

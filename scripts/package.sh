#!/usr/bin/env bash
# Build one self-contained release archive for a runtime identifier.
# Usage: scripts/package.sh <rid> [output-dir]
set -euo pipefail

RID="${1:?usage: package.sh <rid> [output-dir]}"
OUT="${2:-dist}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$ROOT/src/Deskboard.App/Deskboard.App.csproj"
STAGE="$OUT/stage-$RID"
NAME="deskboard-native-$RID"
VERSION="${VERSION:-0.2.1}"

rm -rf "$STAGE"; mkdir -p "$STAGE/$NAME"

dotnet publish "$APP" -c Release -r "$RID" --self-contained true -o "$STAGE/publish" >/dev/null

case "$RID" in
  linux-*)
    cp -r "$STAGE/publish/." "$STAGE/$NAME/"
    cp "$ROOT/assets/deskboard-native.desktop" "$STAGE/$NAME/"
    cp "$ROOT/assets/icon.png" "$STAGE/$NAME/"
    cp "$ROOT/assets/icon.png" "$STAGE/$NAME/deskboard-native.png"
    cat > "$STAGE/$NAME/deskboard-native" <<'LAUNCH'
#!/usr/bin/env bash
DIR="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
cd "$DIR"
exec "$DIR/Deskboard.App" "$@"
LAUNCH
    chmod +x "$STAGE/$NAME/deskboard-native" "$STAGE/$NAME/Deskboard.App"
    tar -C "$STAGE" -czf "$OUT/$NAME.tar.gz" "$NAME"
    echo "$OUT/$NAME.tar.gz"
    ;;
  win-*)
    cp -r "$STAGE/publish/." "$STAGE/$NAME/"
    (cd "$STAGE" && zip -qr "$OUT/$NAME.zip" "$NAME")
    echo "$OUT/$NAME.zip"
    ;;
  osx-*)
    APPDIR="$STAGE/$NAME/Deskboard.app"
    mkdir -p "$APPDIR/Contents/MacOS" "$APPDIR/Contents/Resources"
    cp -r "$STAGE/publish/." "$APPDIR/Contents/MacOS/"
    cp "$ROOT/assets/icon.png" "$APPDIR/Contents/Resources/AppIcon.png"
    cat > "$APPDIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Deskboard</string>
  <key>CFBundleIdentifier</key><string>dev.wrecht.deskboard.native</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleExecutable</key><string>Deskboard.App</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
    chmod +x "$APPDIR/Contents/MacOS/Deskboard.App"
    tar -C "$STAGE" -czf "$OUT/$NAME.tar.gz" "$NAME"
    echo "$OUT/$NAME.tar.gz"
    ;;
  *)
    echo "unsupported rid: $RID" >&2; exit 1 ;;
esac

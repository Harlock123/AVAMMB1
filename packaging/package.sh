#!/usr/bin/env bash
# Makes the macOS disk image (.dmg) and the Linux AppImage from a build in dist/<rid>/ (see build-all.sh).
# Usage: packaging/package.sh <rid> [version]
set -euo pipefail
cd "$(dirname "$0")/.."
rid="$1"
VERSION="${2:-${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n1)}}"
src="dist/$rid"
name="AVAMMB1-$VERSION-$rid"
[[ -d "$src" ]] || { echo "no build in $src - run build-all.sh first" >&2; exit 1; }

case "$rid" in
  osx-*)
    stage="$(mktemp -d)"
    cp -R "$src/AVAMMB1.app" "$stage/"
    for doc in LICENSE README.md CREDITS.md ASSETS_LICENSES.md; do cp "$src/$doc" "$stage/" 2>/dev/null || true; done
    ln -s /Applications "$stage/Applications"
    hdiutil create -volname "AVAMMB1 $VERSION" -srcfolder "$stage" -ov -format UDZO "dist/$name.dmg"
    rm -rf "$stage"
    echo "packaged dist/$name.dmg"
    ;;
  linux-*)
    arch=$([[ "$rid" == linux-arm64 ]] && echo aarch64 || echo x86_64)
    host=$([[ "$(uname -m)" == aarch64 || "$(uname -m)" == arm64 ]] && echo aarch64 || echo x86_64)
    tools="$(mktemp -d)"
    curl -fsSL -o "$tools/appimagetool" "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-$host.AppImage"
    curl -fsSL -o "$tools/runtime" "https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-$arch"
    chmod +x "$tools/appimagetool"
    appdir="$(mktemp -d)/AVAMMB1.AppDir"
    mkdir -p "$appdir/usr/bin" "$appdir/usr/share/doc/avammb1"
    cp "$src/AVAMMB1" "$appdir/usr/bin/"
    chmod +x "$appdir/usr/bin/AVAMMB1"
    for doc in LICENSE README.md CREDITS.md ASSETS_LICENSES.md; do cp "$src/$doc" "$appdir/usr/share/doc/avammb1/" 2>/dev/null || true; done
    cp packaging/linux/AVAMMB1.desktop "$appdir/AVAMMB1.desktop"
    cp Assets/Icons/avammb1.png "$appdir/avammb1.png"
    cat > "$appdir/AppRun" <<'RUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/AVAMMB1" "$@"
RUN
    chmod +x "$appdir/AppRun"
    # --appimage-extract-and-run: no FUSE needed (CI runners and containers often lack it).
    ARCH="$arch" "$tools/appimagetool" --appimage-extract-and-run --no-appstream --runtime-file "$tools/runtime" "$appdir" "dist/$name.AppImage"
    chmod +x "dist/$name.AppImage"
    rm -rf "$tools" "$(dirname "$appdir")"
    echo "packaged dist/$name.AppImage"
    ;;
  *)
    echo "nothing to package for $rid here (Windows installers: packaging/package.ps1)"
    ;;
esac

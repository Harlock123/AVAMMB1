#!/usr/bin/env bash
# Publishes single-file, self-contained AVAMMB1 builds for every supported platform into dist/<rid>/
# and packages each one as dist/AVAMMB1-<version>-<rid>.(zip|tar.gz).
#
# Environment overrides:
#   RIDS="linux-x64 osx-arm64"   only build these runtime identifiers
#   R2R=false                    disable ReadyToRun (faster builds, slower startup)
#   SKIP_TESTS=1                 skip the unit tests
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n1)}"
RIDS="${RIDS:-win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64}"
R2R="${R2R:-true}"
PROJECT=src/AVAMMB1.App/AVAMMB1.App.csproj
DOCS=(LICENSE README.md CREDITS.md ASSETS_LICENSES.md)

echo "AVAM&M build $VERSION for: $RIDS"
if [[ "${SKIP_TESTS:-0}" != "1" ]]; then
  dotnet test AVAMMB1.sln -c Release
fi

rm -rf dist
mkdir -p dist

zip_dir() { # $1 = source dir, $2 = archive path
  local src="$1" out="$2"
  if command -v zip >/dev/null 2>&1; then
    (cd "$src" && zip -qr "$out" .)
  else
    python3 - "$src" "$out" <<'PY'
import os, sys, zipfile
src, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(src):
        for f in files:
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, src))
PY
  fi
}

for rid in $RIDS; do
  out="dist/$rid"
  echo "==> Publishing $rid"
  dotnet publish "$PROJECT" -c Release -r "$rid" \
    -p:PublishSingleFile=true -p:SelfContained=true -p:PublishReadyToRun="$R2R" \
    -p:Version="$VERSION" -o "$out"
  cp "${DOCS[@]}" "$out/"
  name="AVAMMB1-$VERSION-$rid"
  case "$rid" in
    win-*)
      zip_dir "$out" "$PWD/dist/$name.zip"
      ;;
    osx-*)
      app="$out/AVAMMB1.app"
      mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
      mv "$out/AVAMMB1" "$app/Contents/MacOS/AVAMMB1"
      chmod +x "$app/Contents/MacOS/AVAMMB1"
      cp Assets/Icons/avammb1.icns "$app/Contents/Resources/"
      sed "s/__VERSION__/$VERSION/g" packaging/macos/Info.plist > "$app/Contents/Info.plist"
      if [[ "$(uname)" == "Darwin" ]]; then
        codesign --force --deep -s - "$app" || echo "warning: ad-hoc codesign failed"
      fi
      tar -C "$out" -czf "dist/$name.tar.gz" .
      ;;
    *)
      chmod +x "$out/AVAMMB1"
      tar -C "$out" -czf "dist/$name.tar.gz" .
      ;;
  esac
  echo "    packaged dist/$name"
done

echo "Done. Artifacts:"
ls -lh dist/*.zip dist/*.tar.gz 2>/dev/null || true

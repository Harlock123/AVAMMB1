#!/usr/bin/env bash
# Writes a ready-to-build Flatpak manifest (and its files) for AVAM&M into OUT_DIR.
# Usage: packaging/flatpak/make-manifest.sh OUT_DIR ARCHIVE                  (a local linux tar.gz - CI bundles)
#        packaging/flatpak/make-manifest.sh OUT_DIR URL SHA256 [x86_64|aarch64] [URL SHA256 ...]
#                                                                           (GitHub release downloads - Flathub)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$1"; shift
id=io.github.Harlock123.AVAMMB1
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -1)"
mkdir -p "$out"
if [[ $# -eq 1 ]]; then
  cp "$1" "$out/game.tar.gz"
  source="      - type: archive
        path: game.tar.gz
        strip-components: 0"
else
  source=""
  while [[ $# -ge 3 ]]; do
    source+="      - type: archive
        url: $1
        sha256: $2
        strip-components: 0
        only-arches: [$3]
"
    shift 3
  done
  source="${source%$'\n'}"
fi
python3 - "$root/packaging/flatpak/$id.yml.in" "$out/$id.yml" "$source" <<'PY'
import sys
template, target, source = sys.argv[1:4]
open(target, "w").write(open(template).read().replace("@GAME_SOURCE@", source))
PY
sed -e "s/@VERSION@/$version/g" -e "s/@DATE@/$(date -u +%F)/g" "$root/packaging/flatpak/$id.metainfo.xml.in" > "$out/$id.metainfo.xml"
cp "$root/packaging/flatpak/$id.desktop" "$out/"
cp "$root/Assets/Icons/avammb1.png" "$out/"
echo "wrote $out/$id.yml"

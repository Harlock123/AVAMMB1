#!/usr/bin/env bash
# Makes the save-game fixture for a release: builds tools/save-fixture against that release's game code
# and writes tests/AVAMMB1.Tests/Fixtures/Saves/v<version>.json.
# Usage: tools/make-save-fixture.sh [git-ref]   (default HEAD - run it after bumping the version)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
ref="${1:-HEAD}"
work="$(mktemp -d)"
trap 'git -C "$root" worktree remove --force "$work/src" >/dev/null 2>&1 || true; rm -rf "$work"' EXIT
git -C "$root" worktree add -q --detach "$work/src" "$ref"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$work/src/Directory.Build.props" | head -1)"
mkdir -p "$work/tool"
cp "$root/tools/save-fixture/Program.cs" "$work/tool/"
cat > "$work/tool/SaveFixture.csproj" <<PROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$work/src/src/AVAMMB1.Core/AVAMMB1.Core.csproj" />
  </ItemGroup>
</Project>
PROJ
dotnet run --project "$work/tool/SaveFixture.csproj" -v q -- "$work/out" >/dev/null
cp "$work/out/slot1.json" "$root/tests/AVAMMB1.Tests/Fixtures/Saves/v$version.json"
echo "wrote tests/AVAMMB1.Tests/Fixtures/Saves/v$version.json"

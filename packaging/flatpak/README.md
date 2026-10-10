# Flatpak

`io.github.Harlock123.AVAMMB1` - AVAM&M as a Flatpak, for Linux desktops and the Steam Deck.

* **CI** builds a bundle for x86_64 and aarch64 from the release's Linux archive (job `flatpak` in
  `.github/workflows/release.yml`), installs it and runs `--smoke-test`; tagged releases attach
  `AVAMMB1-<version>-<arch>.flatpak`. Install one with `flatpak install --user AVAMMB1-*.flatpak`
  (the Freedesktop 24.08 runtime comes from Flathub).
* **Locally** (needs `flatpak-builder` and the Flathub remote):

  ```bash
  packaging/flatpak/make-manifest.sh build/flatpak dist/AVAMMB1-<version>-linux-x64.tar.gz
  flatpak-builder --user --install --install-deps-from=flathub --force-clean build/flatpak-app \
      build/flatpak/io.github.Harlock123.AVAMMB1.yml
  flatpak run io.github.Harlock123.AVAMMB1
  ```

## Submitting to Flathub

Flathub listings are submitted by the project owner as a pull request to
[flathub/flathub](https://github.com/flathub/flathub) (see the
[submission guide](https://docs.flathub.org/docs/for-app-authors/submission)). Make the manifest from a
published release - the archives are downloaded by URL and checked by SHA-256:

```bash
v=<version>; base=https://github.com/Harlock123/AVAMMB1/releases/download/v$v
packaging/flatpak/make-manifest.sh flathub \
  $base/AVAMMB1-$v-linux-x64.tar.gz   "$(curl -sL $base/AVAMMB1-$v-linux-x64.tar.gz | sha256sum | cut -d' ' -f1)" x86_64 \
  $base/AVAMMB1-$v-linux-arm64.tar.gz "$(curl -sL $base/AVAMMB1-$v-linux-arm64.tar.gz | sha256sum | cut -d' ' -f1)" aarch64
```

The `flathub/` folder then holds the manifest, desktop file, AppStream metainfo and icon. Flathub
reviewers may ask for the game to be built from source rather than from release archives; the
manifest's single module is the place to change if so.

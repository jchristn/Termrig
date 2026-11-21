# Releasing Termrig

Installers are built by `src/Termrig.Publisher` from [`publisher.json`](publisher.json) and published by
[`.github/workflows/release.yml`](.github/workflows/release.yml) when a `v<version>` tag is pushed. This follows the
shared installer standard (`requirements/INSTALLERS.md` in the Agents repository). The builds are unsigned by policy;
macOS bundles and binaries are ad-hoc signed so they run at all.

## What a release contains

19 files: for Windows x64 and ARM64, a setup `.exe` and a `.zip`; for macOS x64 and ARM64, a `.pkg`, a `.dmg`, and a
`.tar.gz`; for Linux x64 and ARM64, a `.deb`, an `.rpm`, an AppImage, and a `.tar.gz`; plus `SHA256SUMS`. The release
job refuses to publish if any file is missing (`--channel verify-assets`). The title is `Termrig <version>`, and the
notes open with the install instructions from [`.github/release-install-notes.md`](.github/release-install-notes.md),
followed by the version's `CHANGELOG.md` section. Releases get the **Latest** badge; a version with a suffix such as
`-alpha` or `-rc.1` is marked **Pre-release** (`prereleaseBeforeOne` in `publisher.json` is `false`, so plain 0.x
versions are Latest).

Every format installs the one self-contained `Termrig` executable (plus the bundled font licenses), a menu entry
(`ai.latticetech.termrig`), and the `trig` command: a `/usr/local/bin/trig` symlink from the `.pkg`, a `/usr/bin/trig`
symlink from the `.deb` and `.rpm`, and a `trig.cmd` shim beside `Termrig.exe` on the Windows `PATH`. Launchers set
`TERMRIG_FOREGROUND=1` (from `launchEnvironment` in `publisher.json`) so the Dock, the Linux dock, and the AppImage
track the window they started; `trig` from a terminal detaches as before.

## Steps

1. **Choose the version (human decision).** Agents never change versions on their own. The version lives only in
   `src/Termrig.App/Termrig.App.csproj` (`<Version>`, with `AssemblyVersion` and `FileVersion` beside it). Also update
   the README status line and `packaging/macos/Info.plist` (source builds).
2. **Update `CHANGELOG.md`.** Move the entries under `## [Unreleased]` into `## [<version>] - <date>` and leave an
   empty `## [Unreleased]` above it. The heading must start with exactly `## [<version>]`.
3. **Build your OS's installers locally (optional).** `./build-installers.sh` on macOS or Linux, `build-installers.bat`
   on Windows. Output lands in `installers/<version>/`. Install the result, open Termrig from the menu, and run `trig`
   in a new terminal. Linux needs `fpm`, `rpmbuild`, and `appimagetool`; Windows needs Inno Setup 6.3 or later
   (`choco install innosetup`). The Publisher's preflight prints the install command for anything missing.
4. **Commit and push to `main`, and wait for the `Tests` workflow.** The `Release check` workflow also builds every
   installer, without publishing, whenever packaging files change.
5. **Dry-run the release workflow** and download its artifacts:

   ```sh
   gh workflow run release.yml --ref main
   gh run watch "$(gh run list --workflow release.yml --limit 1 --json databaseId -q '.[0].databaseId')"
   gh run download <run-id> --dir "$(mktemp -d)"
   ```

6. **Install the dry-run artifacts (human).** At least one Windows machine, one Apple Silicon Mac, and one Linux
   distribution: install, open Termrig from the menu, run `trig` in a new terminal, and uninstall. To see the
   Gatekeeper and SmartScreen prompts, move the files through a browser.
7. **Tag and push.**

   ```sh
   git tag -a v<version> -m "Termrig <version>"
   git push origin v<version>
   ```

8. **Verify the release.**

   ```sh
   v=<version>
   gh release view "v$v"
   dir="$(mktemp -d)" && gh release download "v$v" --dir "$dir" && (cd "$dir" && shasum -a 256 -c SHA256SUMS)
   ```

   Check that the title is `Termrig <version>`, the notes open with **Installing** with no `{version}` left, all 19
   files are present, the tag points at the release commit (`git rev-list -n 1 v<version>`), and the release is marked
   **Latest** (or **Pre-release** for a suffixed version).

## When something goes wrong

- **An OS job fails:** nothing was published. Fix `main`, then move the tag:
  `git tag -d v<version> && git push origin :refs/tags/v<version>`, re-tag, and push. For a flaky download, rerun only
  the failed jobs with `gh run rerun <run-id> --failed`.
- **The release job fails:** usually a tag/version mismatch or a missing changelog section. Fix, commit, and re-tag.
- **A published release is broken:** if nobody could have downloaded it yet, `gh release delete v<version>
  --cleanup-tag --yes` and release again. Otherwise leave its files alone and ship a patch version.

## Not yet enabled

Package-manager channels (winget, Chocolatey, Scoop, Homebrew, apt/yum repositories, the NuGet global tool) are not
wired yet. They need free accounts and tokens, and some need external review. Add them behind an
`ENABLE_PACKAGE_MANAGERS` repository variable, one channel at a time, and list a command in the README only once it
works.

Code signing is wired but off (`signing.*.isConfigured: false` in `publisher.json`). Turning it on needs purchased
certificates and is a policy decision, not a code change. If it is ever turned on, remove the bypass instructions from
the release notes template, `INSTALL.md`, the README, and the disk-image note.

Size: the self-contained single-file builds are about 120 MB uncompressed and 45 to 53 MB per macOS installer. They
are not trimmed: Avalonia, CommunityToolkit.Mvvm, and the JSON-backed settings use reflection, and trimming has not
been validated against them. `EnableCompressionInSingleFile` would shrink the installed binary but not the downloads,
which are already compressed, and it adds startup time.

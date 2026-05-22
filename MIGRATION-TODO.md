# SparkleShare .NET 10 migration

Branch: `migrate/net8-phase0` (and follow-up branches per phase)

Build the migrated backend:

```bash
dotnet build SparkleShare.Core.sln -c Release
dotnet test SparkleShare.Core.sln -c Release
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) (LTS since November 2025).

Legacy `SparkleShare.sln` (Mac / Windows / Linux UI) does not build until platform projects are migrated (Phase 1+).

---

## Phase 0 — Foundation (backend)

**Estimate (1 developer + Cursor): 5–8 person-days total**

### Done (commit: Phase 0 initial)

- [x] Git branch `migrate/net8-phase0`
- [x] `Sparkles` → SDK-style `net10.0`
- [x] `Sparkles.Git` → SDK-style `net10.0`
- [x] `Sparkles.Tests` → SDK-style `net10.0`, NUnit via PackageReference
- [x] `SparkleShare.Core.sln` (core libraries + tests only)
- [x] `Directory.Build.props`, `global.json`
- [x] CI workflow `.github/workflows/dotnet-core.yml`
- [x] Cursor rules `.cursor/rules/sparkleshare-migration.mdc`
- [x] All 9 unit tests passing on `net10.0`
- [x] Build warnings fixed: `Thread.Abort`, `WebRequest`, `TimeZone`

### Still open (Phase 0)

- [ ] Extract `SparkleShare/Common` from shared project (`.shproj`) into `SparkleShare.Common` class library (`net10.0`)
- [ ] Add `SparkleShare.Common` to `SparkleShare.Core.sln`
- [ ] Expand test coverage for `Sparkles.Git` (repos, fetch, config)
- [ ] Document build in `README.md` (Core solution vs legacy solution)
- [ ] Decide legacy bridge: multi-target `Sparkles` for old UI builds vs. accept broken `SparkleShare.sln` until Phase 1

---

## Phase 1 — macOS

**Estimate: 7–11 person-days (with Cursor)**

### Done (rough migration, branch `migrate/phase1-mac`)

- [x] SDK project `SparkleShare.Mac` → `net10.0-macos` (SDK-style csproj)
- [x] `Microsoft.macOS` workload (requires `dotnet workload install macos`)
- [x] `Info.plist` minimum macOS 12.0 (SDK requirement)
- [x] Build succeeds with `MD_APPLE_SDK_ROOT=/Applications/Xcode.app` (see `scripts/build-mac.sh`)
- [x] Fix `NSPanelButtonType` → modal return value in `EventLog.cs`
- [x] Debug launch reaches app initialization using system `git` fallback when bundled git is absent
- [x] Replace deprecated `WebView` with `WKWebView`
- [x] Reduce obsolete .NET/AppKit warnings where APIs have direct modern replacements
- [x] Address remaining AppKit deprecations (`NSWindow`, `NSBox`, `NSSavePanel`, `NSAttributedString`) — build is now warning-free
- [x] Prevent duplicate instances (macOS: `NSRunningApplication`; fallback: PID file in config dir)
- [x] Bundle git + LFS in app resources (`postBuild.sh` + `checkGit.sh` / Dugite arm64+x64 in `git.download`; per-arch DMGs via `RID=osx-arm64|osx-x64` and `scripts/release-mac-dmgs.sh`)
- [x] Codesign + notarization pipeline (`scripts/sign-pack-notarize-mac.sh` + `sign-pack-notarize-mac.local.sh.example`; Release DMG with Applications link, `notarytool`, staple DMG)

### Still open (Phase 1)

- [ ] Manual QA (tray, setup, sync, wake-from-sleep)
- [ ] Improve cold-start responsiveness: batch repository initialization and throttle status item/menu updates
- [ ] Login item: replace AppleScript with `SMAppService` where appropriate
- [ ] Register `sparkleshare://` URL scheme on macOS (replace removed legacy `SparkleShareInviteOpener.app`; download invite XML to `~/SparkleShare/` and open setup — see `sparkleshare://addProject/` in `BaseController` / protocol-handler-test)
- [ ] Add `SparkleShare.Mac` to `SparkleShare.Core.sln` and CI (macOS runner; legacy `SparkleShare.sln` still lists Mac but does not build the migrated stack)

---

## Phase 2 — Windows

**Estimate: 6–10 person-days (with Cursor)**

Branch: `migrate/windows-net10`

### Done (initial port from uenz fork)

- [x] `SparkleShare.Windows` → SDK `net10.0-windows` (WinForms + WPF, shared `SparkleShare.projitems`)
- [x] Portable Git bundle via `postBuild.cmd` / `git.download` → `git_scm` in output
- [x] `Command.SetSearchPath` for bundled Git on Windows
- [x] WiX v4 installer (`SparkleShare.Windows.Installer`, product version 4.0.2)
- [x] `scripts/build-windows.cmd` (app + optional `installer` argument)
- [x] Projects in `SparkleShare.Core.sln`

### Still open (Phase 2)

- [ ] Build + MSI on a Windows machine (CI job or manual QA)
- [ ] Tray icon, protocol handler (`sparkleshare://`; still separate `SparkleShareInviteOpener.exe`)
- [ ] Unify WinForms shell + WPF UI (or WPF-only entry)
- [ ] Port optional uenz extras (e.g. `ScpUri`)
- [ ] Remove legacy `SparkleShare.sln` / .NET 4.5 Windows project artifacts when no longer needed

---

## Phase 3 — Linux

**Estimate: 10–18 person-days (with Cursor)**

- [ ] Mono → .NET 10 for Linux project
- [ ] GTK# / Meson: update or replace bindings (notify, soup, webkit2gtk)
- [ ] `.desktop`, AppData, autostart
- [ ] CI Linux package build

---

## Phase 4 — Hardening

**Estimate: 4–6 person-days (with Cursor)**

- [ ] Unified CI for Mac / Windows / Linux releases
- [ ] Security review (SSH, TLS, paths)
- [ ] Crash reporting / logging consistency
- [ ] Release process documentation

---

## Optional — Phase 5 (Avalonia unified UI)

**Estimate: +20–30 person-days (with Cursor)**

- [ ] Single cross-platform UI replacing three native stacks
- [ ] Only after Phases 1–3 or if UI maintenance cost is too high

---

## Notes

- Person-day estimates are for a full phase including QA and packaging, not for a single AI-assisted coding session.
- Phase 0 “initial” delivered the **smallest shippable slice**: migratable core DLLs + green tests. Remaining Phase 0 items are listed above.

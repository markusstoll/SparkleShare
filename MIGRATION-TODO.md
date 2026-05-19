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

### Still open (Phase 0)

- [ ] Extract `SparkleShare/Common` from shared project (`.shproj`) into `SparkleShare.Common` class library (`net10.0`)
- [ ] Add `SparkleShare.Common` to `SparkleShare.Core.sln`
- [ ] Fix or track build warnings: `Thread.Abort`, `WebRequest`, `TimeZone`
- [ ] Expand test coverage for `Sparkles.Git` (repos, fetch, config)
- [ ] Document build in `README.md` (Core solution vs legacy solution)
- [ ] Decide legacy bridge: multi-target `Sparkles` for old UI builds vs. accept broken `SparkleShare.sln` until Phase 1

---

## Phase 1 — macOS

**Estimate: 7–11 person-days (with Cursor)**

- [ ] New SDK project `SparkleShare.Mac` → `net10.0-macos`
- [ ] Replace Xamarin.Mac with `Microsoft.macOS` workload
- [ ] Port AppKit UI (~12 files), `MainMenu.xib`, resources
- [ ] Port `SparkleMacWatcher` (FSEvents)
- [ ] Bundle git + LFS in app resources
- [ ] Login item: replace AppleScript with `SMAppService` where appropriate
- [ ] Codesign + notarization pipeline
- [ ] Manual QA: tray, setup, sync, wake-from-sleep

---

## Phase 2 — Windows

**Estimate: 6–10 person-days (with Cursor)**

- [ ] `SparkleShare.Windows` → `net10.0-windows`
- [ ] Unify WinForms shell + WPF UI (or WPF-only entry)
- [ ] Tray icon, protocol handler, invite opener
- [ ] Installer / packaging

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

# SparkleShare for Windows

SparkleShare on Windows is a **.NET 10** WinForms/WPF app with a **WiX v4** MSI installer. Shared logic lives in `SparkleShare/Common` (`SparkleShare.projitems`); this folder is the Windows UI and packaging head.

## Prerequisites

| Requirement | Notes |
|-------------|--------|
| **Windows 10/11** | Required to run the app, bundle Git, and build the MSI |
| **[.NET SDK 10](https://dotnet.microsoft.com/download)** or newer | `dotnet --version` should report 10.x |
| **Network** (first build) | `postBuild.cmd` downloads Portable Git from the URL in `git.download` |

WiX is **not** installed separately: the installer project pulls **WixToolset.Sdk 4** and extensions via NuGet.

Optional for manual Git setup: edit `git.download` (URL + SHA-256 of the PortableGit `.7z.exe`).

## Mac vs Windows

| Task | macOS | Windows |
|------|-------|---------|
| Compile `SparkleShare.Windows.csproj` | Yes (`EnableWindowsTargeting` in the csproj) | Yes |
| Download/extract bundled Git (`git_scm`) | No (`postBuild.cmd` runs only on Windows) | Yes |
| Run / test the GUI | No | Yes |
| Build MSI installer | No | Yes |

On a Mac you can sanity-check that the project compiles:

```bash
dotnet build SparkleShare/Windows/SparkleShare.Windows.csproj -c Release
```

Output is under `SparkleShare/Windows/bin/Release/` but **without** `git_scm`. For a release-quality build, use a Windows PC, VM, or a CI job on `windows-latest`.

## Building the application

From the **repository root** in `cmd.exe` or PowerShell:

```bat
scripts\build-windows.cmd
```

Equivalent:

```bat
dotnet build SparkleShare\Windows\SparkleShare.Windows.csproj -c Release
```

After a successful build you should have:

```
SparkleShare\Windows\bin\Release\
  SparkleShare.exe
  git_scm\          (Portable Git; created on first Windows build)
  Images\
  Presets\
```

`postBuild.cmd` runs automatically after `dotnet build` on Windows when `git_scm` is missing. It uses `curl`, `certutil`, and the self-extracting PortableGit archive listed in `git.download`.

Legacy entry point (delegates to the script above):

```bat
SparkleShare\Windows\build.cmd
```

## Building the MSI installer

Requires a **Release** app output at `SparkleShare\Windows\bin\Release\` (run the app build first).

```bat
scripts\build-windows.cmd installer
```

Or:

```bat
dotnet build SparkleShare\Windows\Installer\SparkleShare.Windows.Installer.wixproj -c Release -p:Platform=x64
```

Installer output:

```
dist\windows\setup\x64\Release\
```

Product version is defined in `Installer\productVersion.wxi` (keep in sync with `SparkleShare.Windows.csproj` and `Properties\AssemblyAttributes.cs`).

## Solution layout

- `SparkleShare.Windows.csproj` — `net10.0-windows`, WinExe, references `Sparkles` / `Sparkles.Git`
- `Installer\` — WiX v4 MSI (harvests `bin\Release\`, `git_scm`, `Images`, `Presets`)
- `scripts\build-windows.cmd` — app build; pass `installer` for MSI

Projects are also listed in `SparkleShare.Core.sln` at the repo root.

## Resetting settings

Remove:

- `Documents\SparkleShare`
- `%AppData%\org.sparkleshare.SparkleShare` (folder may be hidden)

## Uninstalling

Use **Settings → Apps** or **Control Panel → Programs** after installing via MSI.

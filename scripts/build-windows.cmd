@echo off
setlocal
set DOTNET_CLI_TELEMETRY_OPTOUT=1
cd /d "%~dp0.."

if not "%1"=="installer" goto :build_only
echo Publishing SparkleShare Windows self-contained x64...
dotnet publish SparkleShare\Windows\SparkleShare.Windows.csproj -c Release -p:BuildInstaller=true
if errorlevel 1 exit /b 1
if not exist "SparkleShare\Windows\bin\Release\publish\git_scm\cmd\git.exe" (
    echo ERROR: bundled git missing under publish\git_scm
    exit /b 1
)
echo Bundled git: SparkleShare\Windows\bin\Release\publish\git_scm\
echo Building WiX installer x64...
dotnet build SparkleShare\Windows\Installer\SparkleShare.Windows.Installer.wixproj -c Release -p:Platform=x64 -p:BuildInstaller=true
if errorlevel 1 exit /b 1
echo MSI: dist\windows\setup\x64\Release\
goto :done

:build_only
echo Building SparkleShare Windows Release...
dotnet build SparkleShare\Windows\SparkleShare.Windows.csproj -c Release
if errorlevel 1 exit /b 1
:done

endlocal

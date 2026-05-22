@echo off
setlocal
set DOTNET_CLI_TELEMETRY_OPTOUT=1
cd /d "%~dp0.."

echo Building SparkleShare Windows Release...
dotnet build SparkleShare\Windows\SparkleShare.Windows.csproj -c Release
if errorlevel 1 exit /b 1

if not "%1"=="installer" goto :done
echo Building WiX installer x64...
dotnet build SparkleShare\Windows\Installer\SparkleShare.Windows.Installer.wixproj -c Release -p:Platform=x64
if errorlevel 1 exit /b 1
echo MSI: dist\windows\setup\x64\Release\
:done

endlocal

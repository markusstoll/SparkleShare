@echo off
setlocal
set ROOT=%~dp0..
set OUT=%ROOT%\SparkleShare\Windows\Installer\redist\MicrosoftEdgeWebview2Setup.exe
set URL=https://go.microsoft.com/fwlink/p/?LinkId=2124703

if exist "%OUT%" (
    echo WebView2 bootstrapper already present: %OUT%
    exit /b 0
)

mkdir "%ROOT%\SparkleShare\Windows\Installer\redist" 2>nul
echo Downloading WebView2 Evergreen bootstrapper...
curl -fsSL -o "%OUT%" "%URL%"
if errorlevel 1 (
    echo ERROR: failed to download WebView2 bootstrapper from %URL%
    exit /b 1
)

echo Saved %OUT%
endlocal

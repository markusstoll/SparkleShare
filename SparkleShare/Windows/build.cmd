@echo off
REM Legacy entry point; use scripts\build-windows.cmd from the repo root.
cd /d "%~dp0..\.."
call scripts\build-windows.cmd %*

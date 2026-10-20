@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildViewer.ps1"
if errorlevel 1 (
  echo.
  echo ERROR: viewer build failed.
  pause
  exit /b 1
)
echo.
echo Viewer build completed.
pause

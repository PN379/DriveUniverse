@echo off
REM ============================================================
REM  DriveUniverse — FRESH BUILD (no user data)
REM  Clears all settings, logs, then builds and runs clean.
REM ============================================================

echo.
echo  Clearing user data...
echo.

REM Clear AppData
set "APPDATA_DU=%APPDATA%\DriveUniverse"
if exist "%APPDATA_DU%\settings.xml" del /q "%APPDATA_DU%\settings.xml"
if exist "%APPDATA_DU%\log.txt" del /q "%APPDATA_DU%\log.txt"
echo  Cleared: settings.xml, log.txt

REM Kill any running instance
taskkill /im DriveUniverse.exe /f 2>nul

REM Clean build output
echo.
echo  Cleaning build output...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul

REM Build
echo.
echo  Building fresh...
echo.
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo  BUILD FAILED
    pause
    exit /b 1
)

echo.
echo  ============================================================
echo  FRESH BUILD COMPLETE
echo  No settings, no logs, no history — totally clean.
echo  ============================================================
echo.
echo  Launching DriveUniverse...
echo.
"bin\Release\net8.0-windows\DriveUniverse.exe"

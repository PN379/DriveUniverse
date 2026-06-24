@echo off
REM ============================================================
REM  DriveUniverse — one-click build + run
REM ============================================================
echo Building DriveUniverse...
rmdir /s /q bin 2>nul
rmdir /s /q obj 2>nul
dotnet build -c Release
if errorlevel 1 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo.
echo Build succeeded! Launching...
echo.
"bin\Release\net8.0-windows\DriveUniverse.exe"

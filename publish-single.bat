@echo off
REM ============================================================
REM  DriveUniverse — build a SINGLE self-contained .exe
REM  No .NET install needed to run the result.
REM ============================================================

echo.
echo  Building single-file DriveUniverse.exe ...
echo  (This downloads the runtime once, ~150MB, and takes a few minutes.)
echo.

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

echo.
echo  ============================================================
echo  DONE! Your single exe is at:
echo.
echo    bin\Release\net8.0-windows\win-x64\publish\DriveUniverse.exe
echo.
echo  Copy that ONE file anywhere and run it. No other files needed.
echo  ============================================================
echo.
pause

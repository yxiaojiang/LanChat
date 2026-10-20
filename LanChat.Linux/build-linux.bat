@echo off
setlocal
echo ========================================================
echo       Building LanChat for Linux (x86_64 and ARM64)
echo ========================================================
echo.

echo [1/2] Building for Linux x86_64 (Intel/AMD)...
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ../release/linux-x64

echo.
echo [2/2] Building for Linux ARM64 (aarch64 / Raspberry Pi)...
dotnet publish -c Release -r linux-arm64 --self-contained false -p:PublishSingleFile=true -o ../release/linux-arm64

echo.
echo ========================================================
echo Build complete! Binaries located at:
echo   - release/linux-x64/lanchat
echo   - release/linux-arm64/lanchat
echo   - lanchat.py (Universal zero-dependency Python edition)
echo ========================================================
echo.
pause

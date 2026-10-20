@echo off
setlocal
echo ========================================================
echo       LanChat All-in-One Multiplatform Packaging
echo ========================================================
echo.

echo [1/3] Building Windows Executable and MSI Installer...
call build-msi.bat 1

echo.
echo [2/3] Publishing Self-Contained Linux Binaries...
cd LanChat.Linux
call build-linux.bat 1
cd ..

echo.
echo [3/3] Generating Linux Debian Packages (.deb)...
python build-deb.py

echo.
echo ========================================================
echo Packaging Complete! All installers are ready:
echo.
echo Windows:
echo   - LanChat.exe (Standalone ~46KB)
echo   - LanChat.msi (Windows Installer ~65KB)
echo   - release\LanChat.msi
echo.
echo Linux:
echo   - release\LanChat.deb (Debian / Ubuntu / Kylin)
echo   - release\lanchat_1.0.0_amd64.deb
echo   - release\lanchat_1.0.0_all.deb
echo   - release\linux-x64\lanchat (Self-contained ELF)
echo   - release\linux-arm64\lanchat (Self-contained ELF)
echo   - lanchat.py (Universal zero-dependency Python edition)
echo ========================================================
echo.
if "%~1"=="" (
    pause
)

@echo off
setlocal
echo ========================================================
echo       LanChat All-in-One Multiplatform Packaging
echo ========================================================
echo.

echo [1/3] Building Windows Executable and MSI Installer...
call "%~dp0build-msi.bat" 1
if errorlevel 1 exit /b %errorlevel%

echo.
echo [2/3] Publishing Self-Contained Linux Binaries...
call "%~dp0..\src\linux\LanChat.Linux\build-linux.bat" 1
if errorlevel 1 exit /b %errorlevel%

echo.
echo [3/3] Generating Linux Debian Packages (.deb)...
python "%~dp0build-deb.py" --elf-dir "%~dp0..\release"
if errorlevel 1 exit /b %errorlevel%

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
echo   - release\lanchat_*_amd64.deb
echo   - release\lanchat_*_all.deb
echo   - release\linux-x64\lanchat (Self-contained ELF)
echo   - release\linux-arm64\lanchat (Self-contained ELF)
echo   - release\lanchat.pyz (Portable Python edition)
echo ========================================================
echo.
if "%~1"=="" (
    pause
)

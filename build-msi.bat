@echo off
setlocal
echo ========================================================
echo         Building LanChat Windows Installer (.msi)
echo ========================================================
echo.

echo [1/4] Building LanChat.exe ...
call build.bat 1
if %errorlevel% neq 0 (
    echo [ERROR] LanChat.exe build failed.
    exit /b %errorlevel%
)

echo.
echo [2/4] Building uninstall.exe ...
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
"%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:uninstall.exe Uninstall.cs
if %errorlevel% neq 0 (
    echo [ERROR] uninstall.exe build failed.
    exit /b %errorlevel%
)
echo [SUCCESS] uninstall.exe built (~7 KB)

echo.
echo [3/4] Ensuring WiX extensions are present ...
wix extension add -g WixToolset.UI.wixext/5.0.2 >nul 2>nul
wix extension add -g WixToolset.Util.wixext/5.0.2 >nul 2>nul

echo.
echo [4/4] Compiling LanChat.msi with Custom InstallDir UI and Chinese localization ...
wix build Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -culture zh-CN -o LanChat.msi
if %errorlevel% equ 0 (
    copy /y LanChat.msi release\LanChat.msi >nul 2>nul
    echo.
    echo ========================================================
    echo [SUCCESS] LanChat.msi built successfully!
    echo Features included:
    echo   - Installation Directory selection [Browse folder]
    echo   - Simplified Chinese UI wizard
    echo   - Launch LanChat immediately on finish checkbox
    echo   - Bundled uninstall.exe inside install folder
    echo Output: LanChat.msi and release\LanChat.msi
    echo ========================================================
) else (
    echo.
    echo [ERROR] WiX build failed.
)

echo.
if "%~1"=="" (
    pause
)

@echo off
setlocal

echo ====================================================================
echo         LanChat Package MSI and Publish to GitHub Releases
echo ====================================================================
echo.

REM 1. Locate LanChat project root directory
set "PROJECT_DIR="
if exist "%~dp0..\src\MainForm*.cs" set "PROJECT_DIR=%~dp0.."
if not defined PROJECT_DIR (
    if exist "%~dp0..\LanChat\src\MainForm*.cs" set "PROJECT_DIR=%~dp0..\LanChat"
)
if not defined PROJECT_DIR (
    if exist "%~dp0src\MainForm*.cs" set "PROJECT_DIR=%~dp0"
)

if not defined PROJECT_DIR (
    echo [ERROR] Could not find project source file src\MainForm*.cs.
    pause
    exit /b 1
)

cd /d "%PROJECT_DIR%"
set "PROJECT_DIR=%CD%"
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "release" mkdir release >nul 2>nul

REM 2. Compile release\LanChat.exe if not present
if not exist "release\LanChat.exe" (
    echo [INFO] LanChat.exe not found in release dir, compiling now...
    if exist "%CSC_PATH%" (
        "%CSC_PATH%" /nologo /target:winexe /main:LanChat.Program /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\LanChat.exe src\Models.cs src\NetworkService.cs src\MainForm*.cs src\Program.cs src\UI\Controls\*.cs src\UI\Renderers\*.cs src\Services\*.cs
        if errorlevel 1 (
            echo [ERROR] LanChat.exe compilation failed.
            pause
            exit /b 1
        )
    ) else (
        echo [ERROR] System C# compiler not found: %CSC_PATH%
        pause
        exit /b 1
    )
)

if not exist "release\uninstall.exe" (
    if exist "src\Uninstall.cs" (
        "%CSC_PATH%" /nologo /target:winexe /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\uninstall.exe src\Uninstall.cs >nul 2>nul
    )
)

REM 3. Package MSI with WiX 5
echo [1/3] Packaging MSI using WiX 5 (release\LanChat.msi)...
wix extension add -g WixToolset.UI.wixext/5.0.2 >nul 2>nul
wix extension add -g WixToolset.Util.wixext/5.0.2 >nul 2>nul

wix build packaging\Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -culture zh-CN -o release\LanChat.msi
if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [ERROR] WiX failed to build LanChat.msi. Please check WiX environment.
    echo ====================================================================
    pause
    exit /b 1
)
echo       [SUCCESS] LanChat.msi created: release\LanChat.msi
echo.

REM 4. Update Linux deb package if script exists
if exist "packaging\build-deb.py" (
    echo [2/3] Checking and building Linux deb packages...
    where python >nul 2>nul
    if not errorlevel 1 (
        python packaging\build-deb.py >nul 2>nul
        echo       [SUCCESS] Linux deb packages updated.
    ) else (
        echo       [SKIP] Python not found, skipping Linux deb build.
    )
)
echo.

REM 5. Publish to GitHub Releases
echo [3/3] Publishing release and uploading assets to GitHub...
where python >nul 2>nul
if not errorlevel 1 (
    python packaging\publish-release.py %*
    if errorlevel 1 (
        echo.
        echo [WARNING] Exception occurred while uploading to GitHub Releases.
    )
) else (
    echo [ERROR] Python environment not found.
    pause
    exit /b 1
)

echo.
echo ====================================================================
echo  Packaging and publishing finished.
echo ====================================================================
echo.
pause
exit /b 0
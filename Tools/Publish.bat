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

REM Automatically add dotnet tools directory to PATH for WiX CLI if present
if exist "%USERPROFILE%\.dotnet\tools" (
    set "PATH=%USERPROFILE%\.dotnet\tools;%PATH%"
)

if not exist "release" mkdir release >nul 2>nul

REM 2. Automatically bump and synchronize version across all files
echo [0/4] Checking and synchronizing release version...
set "TARGET_VER="
where python >nul 2>nul
if not errorlevel 1 (
    for /f "tokens=*" %%v in ('python packaging\bump-version.py %*') do set "TARGET_VER=%%v"
)
if defined TARGET_VER (
    echo       [SUCCESS] Project version bumped to: v%TARGET_VER%
) else (
    echo       [SKIP] Python not found, using existing project version.
)
echo.

REM 3. Force fresh compilation of release\LanChat.exe and release\uninstall.exe
echo [1/4] Compiling latest LanChat binaries from source...
tasklist /fi "imagename eq LanChat.exe" 2>nul | findstr /i "LanChat.exe" >nul
if not errorlevel 1 (
    echo       Detected running LanChat.exe, terminating process to release file lock...
    taskkill /f /im LanChat.exe >nul 2>nul
    timeout /t 1 /nobreak >nul 2>nul
)
if not exist "%CSC_PATH%" (
    echo [ERROR] System C# compiler not found: %CSC_PATH%
    pause
    exit /b 1
)

"%CSC_PATH%" /nologo /target:winexe /main:LanChat.Program /win32icon:assets\app.ico /res:assets\app.ico,LanChat.app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\LanChat.exe src\Models.cs src\NetworkService*.cs src\MainForm*.cs src\Program.cs src\UI\Controls\*.cs src\UI\Renderers\*.cs src\Services\*.cs
if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [ERROR] LanChat.exe compilation failed. Aborting publish process.
    echo ====================================================================
    pause
    exit /b 1
)

if exist "src\Uninstall.cs" (
    "%CSC_PATH%" /nologo /target:winexe /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\uninstall.exe src\Uninstall.cs >nul 2>nul
)
echo       [SUCCESS] Fresh binaries compiled: release\LanChat.exe
echo.

REM 4. Package MSI with WiX 5
echo [2/4] Packaging MSI using WiX 5 (release\LanChat.msi)...
where wix >nul 2>nul
if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [ERROR] WiX 5 tool not found. Please install it with:
    echo         dotnet tool install --global wix
    echo ====================================================================
    pause
    exit /b 1
)

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

REM 5. Update Linux deb package if script exists
if exist "packaging\build-deb.py" (
    echo [3/4] Checking and building Linux deb packages...
    where python >nul 2>nul
    if not errorlevel 1 (
        python packaging\build-deb.py >nul 2>nul
        echo       [SUCCESS] Linux deb packages updated.
    ) else (
        echo       [SKIP] Python not found, skipping Linux deb build.
    )
)
echo.

REM 6. Publish to GitHub Releases
echo [4/4] Publishing release and uploading assets to GitHub...
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

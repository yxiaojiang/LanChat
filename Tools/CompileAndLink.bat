@echo off
setlocal

echo ====================================================================
echo         LanChat Compile, Link and Hot-Replace Tool
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
set "TARGET_INSTALL_DIR=D:\soft\LanChat"
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo [1/3] Checking environment...
if not exist "%CSC_PATH%" (
    echo [ERROR] System C# compiler not found: %CSC_PATH%
    pause
    exit /b 1
)
echo       C# Compiler: %CSC_PATH%
echo       Source Dir : %PROJECT_DIR%\src
echo       Target Dir : %TARGET_INSTALL_DIR%
echo.

if not exist "release" mkdir release >nul 2>nul

REM 2. Compile LanChat.exe
echo [2/3] Compiling LanChat.exe ...
"%CSC_PATH%" /nologo /target:winexe /main:LanChat.Program /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\LanChat.exe src\Models.cs src\NetworkService*.cs src\MainForm*.cs src\Program.cs src\UI\Controls\*.cs src\UI\Renderers\*.cs src\Services\*.cs

if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [ERROR] LanChat.exe compilation failed. Please check errors above.
    echo ====================================================================
    pause
    exit /b 1
)

if exist "src\Uninstall.cs" (
    "%CSC_PATH%" /nologo /target:winexe /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\uninstall.exe src\Uninstall.cs >nul 2>nul
)

echo       [SUCCESS] LanChat.exe compiled successfully.
echo.

REM 3. Hot-replace files in installation directory
echo [3/3] Synchronizing files to: %TARGET_INSTALL_DIR% ...

if not exist "%TARGET_INSTALL_DIR%" (
    echo       Target directory does not exist, creating: %TARGET_INSTALL_DIR%
    mkdir "%TARGET_INSTALL_DIR%" >nul 2>nul
)

REM Terminate existing LanChat.exe process if running
tasklist /fi "imagename eq LanChat.exe" 2>nul | findstr /i "LanChat.exe" >nul
if not errorlevel 1 (
    echo       Detected running LanChat.exe, terminating process for hot-replace...
    taskkill /f /im LanChat.exe >nul 2>nul
    timeout /t 1 /nobreak >nul 2>nul
)

copy /y release\LanChat.exe "%TARGET_INSTALL_DIR%\LanChat.exe" >nul
if errorlevel 1 (
    echo.
    echo [ERROR] Copy failed. File might be locked or requires administrator privileges.
    pause
    exit /b 1
)

if exist "release\uninstall.exe" (
    copy /y release\uninstall.exe "%TARGET_INSTALL_DIR%\uninstall.exe" >nul 2>nul
)
if exist "assets\app.ico" (
    copy /y assets\app.ico "%TARGET_INSTALL_DIR%\app.ico" >nul 2>nul
)

echo       [SUCCESS] New LanChat.exe copied to: %TARGET_INSTALL_DIR%\LanChat.exe
echo.
echo ====================================================================
echo  Compilation and local update completed successfully.
echo ====================================================================
echo.

set /p RUN_NOW="Launch LanChat now? [Y/N, default=Y]: "
if /i "%RUN_NOW%"=="" set RUN_NOW=Y
if /i "%RUN_NOW%"=="Y" (
    echo Starting: %TARGET_INSTALL_DIR%\LanChat.exe ...
    start "" "%TARGET_INSTALL_DIR%\LanChat.exe"
)

echo.
echo Done.
pause
exit /b 0
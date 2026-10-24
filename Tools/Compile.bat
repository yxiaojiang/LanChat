@echo off
setlocal

echo ====================================================================
echo         LanChat Fast Native Compiler (Compile Only)
echo ====================================================================
echo.

REM 1. Locate LanChat project root directory
set "PROJECT_DIR="
if exist "%~dp0..\src\MainForm.cs" set "PROJECT_DIR=%~dp0.."
if not defined PROJECT_DIR (
    if exist "%~dp0..\LanChat\src\MainForm.cs" set "PROJECT_DIR=%~dp0..\LanChat"
)
if not defined PROJECT_DIR (
    if exist "%~dp0src\MainForm.cs" set "PROJECT_DIR=%~dp0"
)

if not defined PROJECT_DIR (
    echo [ERROR] Could not find project source file src\MainForm.cs.
    pause
    exit /b 1
)

cd /d "%PROJECT_DIR%"
set "PROJECT_DIR=%CD%"
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo [1/2] Checking C# compiler...
if not exist "%CSC_PATH%" (
    echo [ERROR] System C# compiler not found: %CSC_PATH%
    pause
    exit /b 1
)
echo       C# Compiler: %CSC_PATH%
echo       Source Dir : %PROJECT_DIR%\src
echo       Output Dir : %PROJECT_DIR%\release
echo.

if not exist "release" mkdir release >nul 2>nul

echo [2/2] Compiling LanChat.exe ...
"%CSC_PATH%" /nologo /target:winexe /main:LanChat.Program /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\LanChat.exe src\Models.cs src\NetworkService.cs src\MainForm.cs src\Program.cs src\UI\Controls\*.cs src\UI\Renderers\*.cs src\Services\*.cs

if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [ERROR] LanChat.exe compilation failed. Please check code errors above.
    echo ====================================================================
    pause
    exit /b 1
)

if exist "src\Uninstall.cs" (
    "%CSC_PATH%" /nologo /target:winexe /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\uninstall.exe src\Uninstall.cs >nul 2>nul
)

echo       [SUCCESS] LanChat.exe compiled successfully. (Output: release\LanChat.exe)
echo.
echo ====================================================================
echo  Compilation completed successfully. (No files replaced or published)
echo ====================================================================
echo.
pause
exit /b 0
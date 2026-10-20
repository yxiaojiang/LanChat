@echo off
setlocal
echo ========================================================
echo         Building LanChat Windows Installer (.msi)
echo ========================================================
echo.

echo [1/3] Building LanChat.exe ...
call build.bat 1
if %errorlevel% neq 0 (
    echo [ERROR] LanChat.exe build failed.
    exit /b %errorlevel%
)

echo.
echo [2/3] Checking WiX toolset ...
where wix >nul 2>nul
if %errorlevel% neq 0 (
    echo WiX not found in PATH, attempting dotnet tool restore...
    dotnet tool install --global wix --version 5.0.2
)

echo.
echo [3/3] Compiling LanChat.msi ...
wix build Package.wxs -o LanChat.msi
if %errorlevel% equ 0 (
    copy /y LanChat.msi release\LanChat.msi >nul 2>nul
    echo.
    echo ========================================================
    echo [SUCCESS] LanChat.msi built successfully!
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

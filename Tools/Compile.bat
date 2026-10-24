@echo off
setlocal enabledelayedexpansion

echo ====================================================================
echo         LanChat 原生极速编译脚本 (Compile Only)
echo ====================================================================
echo.

REM 1. 自动定位 LanChat 项目根目录
set "PROJECT_DIR="
if exist "%~dp0..\MainForm.cs" set "PROJECT_DIR=%~dp0.."
if not defined PROJECT_DIR (
    if exist "%~dp0..\LanChat\MainForm.cs" set "PROJECT_DIR=%~dp0..\LanChat"
)
if not defined PROJECT_DIR (
    if exist "%~dp0MainForm.cs" set "PROJECT_DIR=%~dp0"
)

if not defined PROJECT_DIR (
    echo [错误] 未能找到项目核心源文件 MainForm.cs。
    pause
    exit /b 1
)

cd /d "%PROJECT_DIR%"
set "PROJECT_DIR=%CD%"
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo [1/2] 检查 C# 编译器环境...
if not exist "%CSC_PATH%" (
    echo [错误] 系统 C# 编译器不存在: %CSC_PATH%
    pause
    exit /b 1
)
echo       C# 编译器: %CSC_PATH%
echo       源码目录: %PROJECT_DIR%
echo.

echo [2/2] 正在编译链接 LanChat.exe ...
"%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:LanChat.exe Models.cs NetworkService.cs MainForm.cs Program.cs

if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [错误] LanChat.exe 编译失败！请检查上述代码报错。
    echo ====================================================================
    pause
    exit /b 1
)

if exist "Uninstall.cs" (
    "%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:uninstall.exe Uninstall.cs >nul 2>nul
)

echo       [成功] LanChat.exe 编译链接完成！(位于项目根目录，约 46 KB)
echo.
echo ====================================================================
echo  纯编译完成！(未覆盖安装目录，未打包发布)
echo ====================================================================
echo.
pause
exit /b 0

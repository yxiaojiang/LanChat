@echo off
setlocal enabledelayedexpansion

echo ====================================================================
echo         LanChat 一键编译链接与覆盖安装工具
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
set "TARGET_INSTALL_DIR=D:\soft\LanChat"
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo [1/3] 检查编译与目标环境...
if not exist "%CSC_PATH%" (
    echo [错误] 系统 C# 编译器不存在: %CSC_PATH%
    pause
    exit /b 1
)
echo       C# 编译器: %CSC_PATH%
echo       源码目录: %PROJECT_DIR%
echo       目标目录: %TARGET_INSTALL_DIR%
echo.

REM 2. 编译 LanChat.exe
echo [2/3] 正在编译链接 LanChat.exe ...
"%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:LanChat.exe Models.cs NetworkService.cs MainForm.cs Program.cs

if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [错误] LanChat.exe 编译失败！请检查上述代码报错。
    echo ====================================================================
    pause
    exit /b 1
)

REM 顺便编译 uninstall.exe
if exist "Uninstall.cs" (
    "%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:uninstall.exe Uninstall.cs >nul 2>nul
)

echo       [成功] LanChat.exe 编译链接完成！
echo.

REM 3. 覆盖替换安装目录
echo [3/3] 正在同步覆盖到已安装目录: %TARGET_INSTALL_DIR% ...

if not exist "%TARGET_INSTALL_DIR%" (
    echo       安装目录不存在，自动创建目录: %TARGET_INSTALL_DIR%
    mkdir "%TARGET_INSTALL_DIR%" >nul 2>nul
)

REM 检测目标程序是否正在运行
tasklist /fi "imagename eq LanChat.exe" 2>nul | findstr /i "LanChat.exe" >nul
if not errorlevel 1 (
    echo       检测到 LanChat.exe 正在运行中，正在退出原进程以便文件替换...
    taskkill /f /im LanChat.exe >nul 2>nul
    timeout /t 1 /nobreak >nul 2>nul
)

copy /y LanChat.exe "%TARGET_INSTALL_DIR%\LanChat.exe" >nul
if errorlevel 1 (
    echo.
    echo [错误] 复制替换失败！文件可能仍被占用或权限不足。
    pause
    exit /b 1
)

if exist "uninstall.exe" (
    copy /y uninstall.exe "%TARGET_INSTALL_DIR%\uninstall.exe" >nul 2>nul
)
if exist "app.ico" (
    copy /y app.ico "%TARGET_INSTALL_DIR%\app.ico" >nul 2>nul
)

echo       [成功] 新版 LanChat.exe 已成功覆盖至: %TARGET_INSTALL_DIR%\LanChat.exe
echo.
echo ====================================================================
echo  编译与本地更新已全部完成！
echo ====================================================================
echo.

set /p RUN_NOW="是否立即启动新版 LanChat? [Y/N，默认Y]: "
if /i "%RUN_NOW%"=="" set RUN_NOW=Y
if /i "%RUN_NOW%"=="Y" (
    echo 正在启动: %TARGET_INSTALL_DIR%\LanChat.exe ...
    start "" "%TARGET_INSTALL_DIR%\LanChat.exe"
)

echo.
echo 操作完成。
pause
exit /b 0

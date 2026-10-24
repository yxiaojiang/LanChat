@echo off
setlocal enabledelayedexpansion

echo ====================================================================
echo         LanChat 一键打包 MSI 并发布至 GitHub Releases
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

REM 2. 检查是否有 LanChat.exe，若没有则自动先编译
if not exist "LanChat.exe" (
    echo [提示] 未在项目根目录检测到 LanChat.exe，正在自动编译...
    if exist "%CSC_PATH%" (
        "%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:LanChat.exe Models.cs NetworkService.cs MainForm.cs Program.cs
        if errorlevel 1 (
            echo [错误] LanChat.exe 编译失败。
            pause
            exit /b 1
        )
    ) else (
        echo [错误] 未找到系统 C# 编译器: %CSC_PATH%
        pause
        exit /b 1
    )
)

REM 确保配套的 uninstall.exe 存在
if not exist "uninstall.exe" (
    if exist "Uninstall.cs" (
        "%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:uninstall.exe Uninstall.cs >nul 2>nul
    )
)

REM 3. WiX 5 工具打包 LanChat.msi
echo [1/3] 正在使用 WiX 5 打包 Windows 安装包 (LanChat.msi)...
wix extension add -g WixToolset.UI.wixext/5.0.2 >nul 2>nul
wix extension add -g WixToolset.Util.wixext/5.0.2 >nul 2>nul

wix build Package.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -culture zh-CN -o LanChat.msi
if errorlevel 1 (
    echo.
    echo ====================================================================
    echo [错误] WiX 构建 LanChat.msi 失败！请检查 WiX 环境。
    echo ====================================================================
    pause
    exit /b 1
)

if not exist "release" mkdir release >nul 2>nul
copy /y LanChat.msi release\LanChat.msi >nul 2>nul
echo       [成功] LanChat.msi 打包完成并同步至 release\LanChat.msi
echo.

REM 4. 可选同步更新 Linux deb 包
if exist "build-deb.py" (
    echo [2/3] 正在检查并生成最新的 Linux deb 安装包...
    where python >nul 2>nul
    if not errorlevel 1 (
        python build-deb.py >nul 2>nul
        echo       [成功] Linux deb 包已同步更新！
    ) else (
        echo       [跳过] 未检测到 python 命令，跳过 Linux deb 打包。
    )
)
echo.

REM 5. 调用 publish-release.py 发布并上传二进制资产到 GitHub Releases
echo [3/3] 正在自动发布到 GitHub Releases 并上传资产...
where python >nul 2>nul
if not errorlevel 1 (
    python publish-release.py %*
    if errorlevel 1 (
        echo.
        echo [警告] 上传 GitHub Releases 时发生异常，请检查网络或 GitHub 凭据。
    )
) else (
    echo [错误] 系统中未找到 Python 运行环境，无法调用 publish-release.py。
    pause
    exit /b 1
)

echo.
echo ====================================================================
echo  打包与发布流程已全部结束！
echo ====================================================================
echo.
pause
exit /b 0

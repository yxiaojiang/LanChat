@echo off
setlocal
echo ========================================================
echo         Building LanChat (Lightweight Windows EXE)
echo ========================================================
echo.

set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if exist "%CSC_PATH%" (
    echo [1/2] Found system C# compiler: %CSC_PATH%
    echo [2/2] Compiling LanChat.exe ...
    "%CSC_PATH%" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:LanChat.exe Models.cs NetworkService.cs MainForm.cs Program.cs
    
    if %errorlevel% equ 0 (
        echo.
        echo ========================================================
        echo [SUCCESS] LanChat.exe built successfully!
        echo Standalone executable size: ~46 KB
        echo Runs on Windows 7/8/10/11 out-of-the-box!
        echo ========================================================
    ) else (
        echo.
        echo [ERROR] Compilation failed.
    )
) else (
    echo System csc.exe not found, trying dotnet CLI...
    dotnet build -c Release
)

echo.
if "%~1"=="" (
    pause
)

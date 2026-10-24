@echo off
setlocal
cd /d "%~dp0.."
set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "release" mkdir release >nul 2>nul
"%CSC_PATH%" /nologo /target:winexe /win32icon:assets\app.ico /r:System.Windows.Forms.dll,System.Drawing.dll,System.dll,System.Core.dll /out:release\LanChat.exe src\Models.cs src\NetworkService.cs src\MainForm.cs src\Program.cs
if "%~1"=="" pause

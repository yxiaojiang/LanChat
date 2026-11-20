@echo off
setlocal
echo ========================================================
echo       Building LanChat for Linux (x86_64 and ARM64)
echo ========================================================
echo.

set "ELF_OUTPUT=%~2"
if not defined ELF_OUTPUT set "ELF_OUTPUT=%~dp0..\..\..\release"
python "%~dp0..\..\..\packaging\build_linux.py" "%ELF_OUTPUT%"
if errorlevel 1 exit /b %errorlevel%
if "%~1"=="" (
    pause
)
exit /b 0

@echo off
setlocal
pushd "%~dp0.."

where python >nul 2>nul
if not errorlevel 1 goto RUN_PYTHON

where py >nul 2>nul
if not errorlevel 1 goto RUN_PY_LAUNCHER

echo [ERROR] Python 3 was not found. Install Python 3 and try again.
popd
pause
exit /b 1

:RUN_PYTHON
python Tools\Scripts\BuildPanelServer.py
set "RESULT=%ERRORLEVEL%"
goto FINISH

:RUN_PY_LAUNCHER
py -3 Tools\Scripts\BuildPanelServer.py
set "RESULT=%ERRORLEVEL%"

:FINISH
popd
if not "%RESULT%"=="0" pause
exit /b %RESULT%

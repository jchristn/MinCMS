@echo off
setlocal

if "%~1"=="" (
    echo Usage: build-all.bat ^<tag^> [namespace]
    echo Example: build-all.bat v1.0.0
    echo Example: build-all.bat v1.0.0 jchristn77
    exit /b 1
)

set TAG=%~1
set NAMESPACE=%~2

pushd "%~dp0"

call build-dashboard.bat "%TAG%" "%NAMESPACE%"
if errorlevel 1 (
    popd
    exit /b 1
)

call build-server.bat "%TAG%" "%NAMESPACE%"
if errorlevel 1 (
    popd
    exit /b 1
)

popd
echo Done.
endlocal

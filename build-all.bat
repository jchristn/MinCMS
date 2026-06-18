@echo off
setlocal

if "%~1"=="" (
    echo Usage: build-all.bat ^<tag^>
    echo Example: build-all.bat v1.0.0
    exit /b 1
)

set TAG=%~1

pushd "%~dp0"

call build-dashboard.bat "%TAG%"
if errorlevel 1 (
    popd
    exit /b 1
)

call build-server.bat "%TAG%"
if errorlevel 1 (
    popd
    exit /b 1
)

popd
echo Done.
endlocal

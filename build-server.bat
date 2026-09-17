@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-server.bat ^<tag^> [namespace]
    echo Example: build-server.bat v1.0.0
    echo Example: build-server.bat v1.0.0 jchristn77
    exit /b 1
)
set TAG=%~1
set NAMESPACE=%~2
if "%NAMESPACE%"=="" set NAMESPACE=jchristn77
set IMAGE=mincms-server

echo Building for linux/amd64 and linux/arm64/v8...
docker buildx build ^
    -f src/MinCms.Server/Dockerfile ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    --tag %NAMESPACE%/%IMAGE%:%TAG% ^
    --tag %NAMESPACE%/%IMAGE%:latest ^
    --push ^
    src/
if errorlevel 1 (
    echo Build failed for %IMAGE%.
    exit /b 1
)

echo Done.
endlocal

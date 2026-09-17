@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-dashboard.bat ^<tag^> [namespace]
    echo Example: build-dashboard.bat v1.0.0
    echo Example: build-dashboard.bat v1.0.0 jchristn77
    exit /b 1
)
set TAG=%~1
set NAMESPACE=%~2
if "%NAMESPACE%"=="" set NAMESPACE=jchristn77
set IMAGE=mincms-dashboard

echo Building for linux/amd64 and linux/arm64/v8...
docker buildx build ^
    -f dashboard/Dockerfile ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    --tag %NAMESPACE%/%IMAGE%:%TAG% ^
    --tag %NAMESPACE%/%IMAGE%:latest ^
    --push ^
    dashboard/
if errorlevel 1 (
    echo Build failed for %IMAGE%.
    exit /b 1
)

echo Done.
endlocal

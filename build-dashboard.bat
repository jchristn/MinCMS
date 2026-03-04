@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-dashboard.bat ^<tag^>
    echo Example: build-dashboard.bat v1.0.0
    exit /b 1
)
set TAG=%~1
set IMAGE=jchristn77/mincms-dashboard
echo Building %IMAGE%:latest and %IMAGE%:%TAG%...
docker buildx build ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -f dashboard/Dockerfile ^
    --push ^
    dashboard/
echo Done.
endlocal

@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-server.bat ^<tag^>
    echo Example: build-server.bat v1.0.0
    exit /b 1
)
set TAG=%~1
set IMAGE=jchristn77/mincms-server
echo Building %IMAGE%:latest and %IMAGE%:%TAG%...
docker buildx build ^
    --builder cloud-jchristn77-jchristn77 ^
    --platform linux/amd64,linux/arm64/v8 ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -f src/MinCms.Server/Dockerfile ^
    --push ^
    src/
echo Done.
endlocal

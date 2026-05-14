@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-server.bat ^<tag^>
    echo Example: build-server.bat v1.0.0
    exit /b 1
)
set TAG=%~1
set IMAGE=mincms-server
echo Building %IMAGE%:latest and %IMAGE%:%TAG% from local source...
docker build ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -f src/MinCms.Server/Dockerfile ^
    src/
echo Done.
endlocal

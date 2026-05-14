@echo off
setlocal
if "%~1"=="" (
    echo Usage: build-dashboard.bat ^<tag^>
    echo Example: build-dashboard.bat v1.0.0
    exit /b 1
)
set TAG=%~1
set IMAGE=mincms-dashboard
echo Building %IMAGE%:latest and %IMAGE%:%TAG% from local source...
docker build ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -f dashboard/Dockerfile ^
    dashboard/
echo Done.
endlocal

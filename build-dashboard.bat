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

echo Building %IMAGE% and %NAMESPACE%/%IMAGE% (:latest, :%TAG%) from local source...
docker build ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -t %NAMESPACE%/%IMAGE%:latest ^
    -t %NAMESPACE%/%IMAGE%:%TAG% ^
    -f dashboard/Dockerfile ^
    dashboard/
if errorlevel 1 (
    echo Build failed for %IMAGE%.
    exit /b 1
)

echo Pushing %NAMESPACE%/%IMAGE%:%TAG% to Docker Hub...
docker push %NAMESPACE%/%IMAGE%:%TAG%
if errorlevel 1 (
    echo Push failed for %NAMESPACE%/%IMAGE%:%TAG%. Are you logged in? Run: docker login
    exit /b 1
)

echo Pushing %NAMESPACE%/%IMAGE%:latest to Docker Hub...
docker push %NAMESPACE%/%IMAGE%:latest
if errorlevel 1 (
    echo Push failed for %NAMESPACE%/%IMAGE%:latest. Are you logged in? Run: docker login
    exit /b 1
)

echo Done.
endlocal

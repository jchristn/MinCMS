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

echo Building %IMAGE% and %NAMESPACE%/%IMAGE% (:latest, :%TAG%) from local source...
docker build ^
    -t %IMAGE%:latest ^
    -t %IMAGE%:%TAG% ^
    -t %NAMESPACE%/%IMAGE%:latest ^
    -t %NAMESPACE%/%IMAGE%:%TAG% ^
    -f src/MinCms.Server/Dockerfile ^
    src/
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

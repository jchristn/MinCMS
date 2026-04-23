@ECHO OFF
SETLOCAL EnableExtensions

SET "SCRIPT_DIR=%~dp0"
FOR %%I IN ("%SCRIPT_DIR%.") DO SET "FACTORY_DIR=%%~fI"
FOR %%I IN ("%SCRIPT_DIR%..") DO SET "DOCKER_DIR=%%~fI"
SET "COMPOSE_MAIN=%DOCKER_DIR%\compose.yaml"
SET "COMPOSE_SERVER=%DOCKER_DIR%\compose-server.yaml"
SET "COMPOSE_DASHBOARD=%DOCKER_DIR%\compose-dashboard.yaml"

ECHO.
ECHO ==========================================================
ECHO   MinCMS Docker Factory Reset
ECHO ==========================================================
ECHO.
ECHO WARNING: This is a destructive reset of the local Docker
ECHO deployment state. The following will be changed:
ECHO.
ECHO   - Docker containers from the MinCMS compose files will be stopped and removed
ECHO   - docker\server\mincms.json will be restored to the factory template
ECHO   - docker\dashboard\entrypoint.sh will be restored to the factory copy
ECHO   - docker\server\logs and docker\dashboard\logs will be cleared
ECHO.
ECHO This reset does NOT delete collections or files already stored
ECHO in the configured S3-compatible bucket.
ECHO.
ECHO After reset, update docker\server\mincms.json with valid S3
ECHO settings before starting the deployment again.
ECHO.
SET /P CONFIRM=Type RESET to continue: 
IF NOT "%CONFIRM%"=="RESET" (
  ECHO.
  ECHO Aborted. No changes were made.
  GOTO :Done
)

ECHO.
ECHO [1/4] Stopping Docker deployments...
CALL :ComposeDown "%COMPOSE_MAIN%"
IF ERRORLEVEL 1 GOTO :Error

CALL :ComposeDown "%COMPOSE_SERVER%"
IF ERRORLEVEL 1 GOTO :Error

CALL :ComposeDown "%COMPOSE_DASHBOARD%"
IF ERRORLEVEL 1 GOTO :Error

docker rm -f mincms-server >NUL 2>&1
docker rm -f mincms-dashboard >NUL 2>&1

ECHO.
ECHO [2/4] Restoring factory config files...
CALL :RestoreFile "mincms_server_config\mincms.json" "%DOCKER_DIR%\server\mincms.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreFile "mincms_dashboard_config\entrypoint.sh" "%DOCKER_DIR%\dashboard\entrypoint.sh"
IF ERRORLEVEL 1 GOTO :Error

ECHO         Restored server and dashboard deployment files

ECHO.
ECHO [3/4] Resetting runtime directories...
CALL :MirrorDirectory "mincms_server_logs" "%DOCKER_DIR%\server\logs"
IF ERRORLEVEL 1 GOTO :Error

CALL :MirrorDirectory "mincms_dashboard_logs" "%DOCKER_DIR%\dashboard\logs"
IF ERRORLEVEL 1 GOTO :Error

ECHO         Cleared local MinCMS log directories

ECHO.
ECHO [4/4] Factory reset complete.
ECHO.
ECHO To restart the deployment:
ECHO   cd /d "%DOCKER_DIR%"
ECHO   docker compose up -d
ECHO.
ECHO Remember to reconfigure docker\server\mincms.json first if you need a working S3 connection.
GOTO :Done

:ComposeDown
SETLOCAL EnableExtensions
SET "COMPOSE_FILE=%~1"

IF EXIST "%COMPOSE_FILE%" (
  docker compose -f "%COMPOSE_FILE%" down --remove-orphans >NUL 2>&1
)

ENDLOCAL & EXIT /B 0

:RestoreFile
SETLOCAL EnableExtensions
SET "RELATIVE_SOURCE=%~1"
SET "TARGET_FILE=%~2"
SET "SOURCE_FILE=%FACTORY_DIR%\%RELATIVE_SOURCE%"

IF NOT EXIST "%SOURCE_FILE%" (
  ECHO Missing factory file: "%SOURCE_FILE%"
  ENDLOCAL & EXIT /B 1
)

FOR %%I IN ("%TARGET_FILE%") DO IF NOT EXIST "%%~dpI" MKDIR "%%~dpI" >NUL 2>&1
COPY /Y "%SOURCE_FILE%" "%TARGET_FILE%" >NUL
IF ERRORLEVEL 1 (
  ENDLOCAL & EXIT /B 1
)

ENDLOCAL & EXIT /B 0

:MirrorDirectory
SETLOCAL EnableExtensions
SET "RELATIVE_SOURCE=%~1"
SET "TARGET_DIR=%~2"
SET "SOURCE_DIR=%FACTORY_DIR%\%RELATIVE_SOURCE%"

IF NOT EXIST "%SOURCE_DIR%" (
  ECHO Missing factory directory: "%SOURCE_DIR%"
  ENDLOCAL & EXIT /B 1
)

IF NOT EXIST "%TARGET_DIR%" MKDIR "%TARGET_DIR%" >NUL 2>&1

robocopy "%SOURCE_DIR%" "%TARGET_DIR%" /MIR /NFL /NDL /NJH /NJS /NP >NUL
SET "ROBOCOPY_EXIT=%ERRORLEVEL%"
IF %ROBOCOPY_EXIT% GEQ 8 (
  ENDLOCAL & EXIT /B 1
)

ENDLOCAL & EXIT /B 0

:Error
ECHO.
ECHO Factory reset failed
EXIT /B 1

:Done
ECHO.
ENDLOCAL
@ECHO ON

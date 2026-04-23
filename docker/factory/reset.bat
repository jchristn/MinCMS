@ECHO OFF
SETLOCAL EnableExtensions

SET "SCRIPT_DIR=%~dp0"
FOR %%I IN ("%SCRIPT_DIR%.") DO SET "FACTORY_DIR=%%~fI"
FOR %%I IN ("%SCRIPT_DIR%..") DO SET "DOCKER_DIR=%%~fI"
SET "COMPOSE_MAIN=%DOCKER_DIR%\compose.yaml"

ECHO.
ECHO ==========================================================
ECHO   MinCMS Docker Factory Reset
ECHO ==========================================================
ECHO.
ECHO WARNING: This is a destructive reset of the local Docker
ECHO deployment state. The following will be changed:
ECHO.
ECHO   - Docker containers from docker\compose.yaml will be stopped and removed
ECHO   - docker\server\mincms.json will be restored to the bundled Less3-backed defaults
ECHO   - docker\dashboard\entrypoint.sh will be restored to the factory copy
ECHO   - docker\less3\system.json and docker\less3\less3.db will be restored
ECHO   - docker\server\logs, docker\dashboard\logs, docker\less3\logs,
ECHO     docker\less3\temp, and docker\less3\disk will be cleared
ECHO.
ECHO After reset, MinCMS will target the bundled Less3 service using
ECHO access key default, secret key default, and bucket default.
ECHO.
SET /P CONFIRM=Type RESET to continue: 
IF NOT "%CONFIRM%"=="RESET" (
  ECHO.
  ECHO Aborted. No changes were made.
  GOTO :Done
)

ECHO.
ECHO [1/4] Stopping Docker deployment...
CALL :ComposeDown "%COMPOSE_MAIN%"
IF ERRORLEVEL 1 GOTO :Error

docker rm -f less3 >NUL 2>&1
docker rm -f less3-ui >NUL 2>&1
docker rm -f mincms-server >NUL 2>&1
docker rm -f mincms-dashboard >NUL 2>&1

ECHO.
ECHO [2/4] Restoring factory config files...
CALL :RestoreFile "mincms_server_config\mincms.json" "%DOCKER_DIR%\server\mincms.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreFile "mincms_dashboard_config\entrypoint.sh" "%DOCKER_DIR%\dashboard\entrypoint.sh"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreFile "less3_config\system.json" "%DOCKER_DIR%\less3\system.json"
IF ERRORLEVEL 1 GOTO :Error

CALL :RestoreFile "less3_database\less3.db" "%DOCKER_DIR%\less3\less3.db"
IF ERRORLEVEL 1 GOTO :Error

DEL /Q "%DOCKER_DIR%\less3\less3.db-shm" >NUL 2>&1
DEL /Q "%DOCKER_DIR%\less3\less3.db-wal" >NUL 2>&1

ECHO         Restored MinCMS and Less3 deployment files

ECHO.
ECHO [3/4] Resetting runtime directories...
CALL :MirrorDirectory "mincms_server_logs" "%DOCKER_DIR%\server\logs"
IF ERRORLEVEL 1 GOTO :Error

CALL :MirrorDirectory "mincms_dashboard_logs" "%DOCKER_DIR%\dashboard\logs"
IF ERRORLEVEL 1 GOTO :Error

CALL :MirrorDirectory "less3_logs" "%DOCKER_DIR%\less3\logs"
IF ERRORLEVEL 1 GOTO :Error

CALL :MirrorDirectory "less3_temp" "%DOCKER_DIR%\less3\temp"
IF ERRORLEVEL 1 GOTO :Error

CALL :MirrorDirectory "less3_disk" "%DOCKER_DIR%\less3\disk"
IF ERRORLEVEL 1 GOTO :Error

ECHO         Cleared local MinCMS and Less3 runtime directories

ECHO.
ECHO [4/4] Factory reset complete.
ECHO.
ECHO To restart the deployment:
ECHO   cd /d "%DOCKER_DIR%"
ECHO   docker compose up -d
ECHO.
ECHO Less3 will be available on http://localhost:8000 and MinCMS on http://localhost:8200.
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

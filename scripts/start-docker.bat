@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Start Docker Infrastructure
echo ========================================
echo.

REM --- Prerequisite: Docker ---
call "%~dp0_check-prereqs.bat" check_docker
if errorlevel 1 (
    echo [ERROR] Docker Desktop is not running.
    echo [INFO] Please start Docker Desktop and try again.
    exit /b 1
)

REM --- Check for required config files ---
set COMPOSE_FILE=%~dp0..\infra\docker-compose.yml
call "%~dp0_check-prereqs.bat" check_file "%~dp0..\infra\.env"
if errorlevel 1 (
    echo [WARN] infra\.env not found. Docker services will use default configuration.
)

if not exist "%COMPOSE_FILE%" (
    echo [ERROR] Docker Compose file not found at %COMPOSE_FILE%
    exit /b 1
)

echo [INFO] Starting Docker Compose services (PostgreSQL 16, Redis 7, Azurite)...
docker compose -f "%COMPOSE_FILE%" up -d
if errorlevel 1 (
    echo [ERROR] Failed to start Docker Compose services.
    exit /b 1
)

echo [INFO] Waiting for all containers to pass health checks...

REM --- Poll health checks for each service ---
REM Uses docker inspect with conditional: if healthcheck defined, check health status;
REM otherwise fall back to running state (handles Azurite which has no healthcheck).
set RETRIES=0
set MAX_RETRIES=30
set ALL_HEALTHY=0

:health_check_loop
if !RETRIES! geq %MAX_RETRIES% (
    echo [ERROR] Not all containers became healthy within the timeout period.
    echo [INFO] Run 'docker compose -f "%COMPOSE_FILE%" ps' to check container status.
    exit /b 1
)

set ALL_HEALTHY=1
set CONTAINERS_SEEN=0

REM IMPORTANT: docker inspect needs a CONTAINER ID, not the compose SERVICE name
REM returned by "ps --services". Passing the service name made inspect fail
REM silently (stderr suppressed), so the inner loop never ran and ALL_HEALTHY
REM was never flipped to 0 - the script reported "healthy" even when nothing
REM could be inspected. Now we resolve real container IDs via "ps -q" and fail
REM closed (treat "couldn't inspect" as not-yet-healthy, not as healthy).
for /f "tokens=1" %%c in ('docker compose -f "%COMPOSE_FILE%" ps -q 2^>nul') do (
    set /a CONTAINERS_SEEN+=1
    set "STATUS="
    for /f "tokens=*" %%h in ('docker inspect --format="{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" %%c 2^>nul') do set "STATUS=%%h"
    if not defined STATUS (
        set ALL_HEALTHY=0
    ) else if not "!STATUS!"=="healthy" if not "!STATUS!"=="running" (
        set ALL_HEALTHY=0
    )
)
if !CONTAINERS_SEEN!==0 set ALL_HEALTHY=0

if not !ALL_HEALTHY!==1 (
    set /a RETRIES+=1
    timeout /t 2 >nul
    goto :health_check_loop
)

echo.
echo ========================================
echo  All Docker services are healthy!
echo ========================================
echo.

REM --- Print container IPs and ports ---
echo [INFO] Service Status:
echo.

set SERVICES_FOUND=0
for /f "tokens=1" %%c in ('docker compose -f "%COMPOSE_FILE%" ps -q 2^>nul') do (
    set /a SERVICES_FOUND+=1
    set "CNAME=%%c"
    for /f "tokens=*" %%n in ('docker inspect --format="{{.Name}}" %%c 2^>nul') do set "CNAME=%%n"
    for /f "tokens=*" %%p in ('docker port %%c 2^>nul ^| findstr /v "^$"') do (
        echo   !CNAME! - %%p
    )
)
if !SERVICES_FOUND!==0 (
    echo [WARN] No services found or failed to query Docker services.
)

echo.
echo [DONE] Docker infrastructure is ready.
echo [INFO] Run scripts\start-api.bat to start the API.

endlocal

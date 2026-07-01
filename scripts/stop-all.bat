@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Stop All Services
echo ========================================
echo.

set COMPOSE_FILE=%~dp0..\infra\docker-compose.yml

REM --- Kill .NET processes that may be running the API ---
echo [INFO] Stopping API (dotnet processes)...
taskkill /F /IM dotnet.exe >nul 2>&1
if errorlevel 1 ( echo [INFO] No dotnet process was running.
) else ( echo [PASS] API processes stopped. )

REM --- Kill Node.js processes (Angular dev server, Metro bundler) ---
echo [INFO] Stopping Node.js processes (Angular, Metro)...
taskkill /F /IM node.exe >nul 2>&1
if errorlevel 1 ( echo [INFO] No Node.js process was running.
) else ( echo [PASS] Node.js processes stopped. )

REM --- Stop Docker Compose services ---
echo [INFO] Stopping Docker Compose services...
if exist "%COMPOSE_FILE%" (
    docker compose -f "%COMPOSE_FILE%" down
    if errorlevel 1 ( echo [WARN] Docker Compose down encountered an issue.
    ) else ( echo [PASS] Docker Compose services stopped. )
) else (
    echo [WARN] Docker Compose file not found at %COMPOSE_FILE%.
)

REM --- Optional: Clean up volumes ---
echo.
echo [INFO] Do you want to remove Docker volumes (database data will be lost)? (Y/N)
set /p CLEANUP_CHOICE=
if /i "!CLEANUP_CHOICE!"=="Y" (
    echo [INFO] Removing Docker volumes...
    docker compose -f "%COMPOSE_FILE%" down -v >nul 2>&1
    if errorlevel 1 ( echo [WARN] Failed to remove Docker volumes.
    ) else ( echo [INFO] Docker volumes removed. )
) else if /i "!CLEANUP_CHOICE!"=="YES" (
    echo [INFO] Removing Docker volumes...
    docker compose -f "%COMPOSE_FILE%" down -v >nul 2>&1
    if errorlevel 1 ( echo [WARN] Failed to remove Docker volumes.
    ) else ( echo [INFO] Docker volumes removed. )
) else (
    echo [INFO] Docker volumes preserved. Data will be available next time.
)

echo.
echo ========================================
echo  All services stopped.
echo ========================================

endlocal

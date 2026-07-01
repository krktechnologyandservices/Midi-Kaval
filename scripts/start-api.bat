@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Start API
echo ========================================
echo.

REM --- Prerequisite: Docker (infrastructure) ---
call "%~dp0_check-prereqs.bat" check_docker
if errorlevel 1 (
    echo [ERROR] Docker is not running. The API requires PostgreSQL, Redis, and Azurite.
    echo [INFO] Run scripts\start-docker.bat first to start the infrastructure.
    exit /b 1
)

REM --- Verify containers are healthy ---
set COMPOSE_FILE=%~dp0..\infra\docker-compose.yml
call "%~dp0_check-prereqs.bat" check_docker_containers "%COMPOSE_FILE%"
if errorlevel 1 (
    echo [ERROR] Docker containers are not healthy.
    echo [INFO] Run scripts\start-docker.bat first to start the infrastructure.
    exit /b 1
)

REM --- Check for required config files ---
call "%~dp0_check-prereqs.bat" check_file "%~dp0..\infra\.env"
if errorlevel 1 (
    echo [WARN] infra\.env not found. API will use default configuration from appsettings.Development.json.
)

REM --- Prerequisite: .NET SDK ---
call "%~dp0_check-prereqs.bat" check_dotnet
if errorlevel 1 (
    echo [ERROR] .NET 8 SDK is required to run the API.
    exit /b 1
)

REM --- Prerequisite: Port 5049 ---
call "%~dp0_check-prereqs.bat" check_port 5049
if errorlevel 1 (
    echo [ERROR] Port 5049 is already in use. The API cannot start.
    echo [INFO] Stop the process using port 5049 or change the API port in configuration.
    exit /b 1
)

REM --- Run the API ---
set API_PROJECT=%~dp0..\apps\api
if not exist "%API_PROJECT%" (
    echo [ERROR] API project not found at %API_PROJECT%
    exit /b 1
)

echo [INFO] Starting API at http://localhost:5049/swagger...
echo [INFO] Press Ctrl+C to stop the API.
echo.
cd /d "%API_PROJECT%"
dotnet run

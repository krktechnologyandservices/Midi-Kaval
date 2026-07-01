@echo off
setlocal enabledelayedexpansion

REM ========================================
REM  Midi-Kaval - Shared Prerequisite Checks
REM ========================================
REM
REM  Usage: CALL "%~dp0_check-prereqs.bat" <check_name> [args...]
REM
REM  Available checks:
REM    check_docker            - Verifies Docker Desktop is running
REM    check_docker_containers - Verifies all Docker Compose containers are healthy
REM    check_dotnet            - Verifies .NET 8 SDK is available
REM    check_node              - Verifies Node.js v18+ is available
REM    check_port <PORT>       - Verifies a given port is free
REM    check_file <PATH>       - Verifies a file exists
REM    check_adb               - Verifies ADB is available
REM    check_adb_device        - Verifies at least one Android device is connected (authorized)
REM    check_api_healthy       - Polls http://localhost:5049/health until ready or timeout
REM ========================================

if "%1"=="" (
    echo [ERROR] No check specified. Usage: CALL "%%~dp0_check-prereqs.bat" ^<check_name^> [args...]
    exit /b 1
)

if "%1"=="check_docker" goto :check_docker
if "%1"=="check_docker_containers" goto :check_docker_containers
if "%1"=="check_dotnet" goto :check_dotnet
if "%1"=="check_node" goto :check_node
if "%1"=="check_port" goto :check_port
if "%1"=="check_file" goto :check_file
if "%1"=="check_adb" goto :check_adb
if "%1"=="check_adb_device" goto :check_adb_device
if "%1"=="check_api_healthy" goto :check_api_healthy

echo [ERROR] Unknown check: %1
echo [INFO] Available checks: check_docker, check_docker_containers, check_dotnet, check_node, check_port, check_file, check_adb, check_adb_device, check_api_healthy
exit /b 1

REM ========================================
:check_docker
REM Verify Docker Desktop is running
echo [CHECK] Checking Docker Desktop...
docker info >nul 2>&1
if errorlevel 1 (
    echo [FAIL] Docker Desktop is not running or not installed.
    echo [INFO] Please start Docker Desktop and try again.
    exit /b 1
)
echo [PASS] Docker Desktop is running.
exit /b 0

REM ========================================
:check_docker_containers
REM Verify all Docker Compose containers are healthy (fallback to running if no healthcheck)
set COMPOSE_FILE=%2
if "%COMPOSE_FILE%"=="" (
    echo [ERROR] No compose file specified. Usage: check_docker_containers ^<COMPOSE_FILE^>
    exit /b 1
)
if not exist "%COMPOSE_FILE%" (
    echo [ERROR] Docker Compose file not found at %COMPOSE_FILE%
    exit /b 1
)

echo [CHECK] Verifying Docker containers are healthy...
set SERVICE_COUNT=0
REM IMPORTANT: "docker compose ps --services" returns SERVICE names (e.g. "postgres"),
REM but "docker inspect" needs the actual CONTAINER ID/name. Using the service name
REM directly caused inspect to fail silently (2^>nul swallowed the error), so this
REM check never actually validated anything. Fix: resolve container IDs via "ps -q".
for /f "tokens=1" %%c in ('docker compose -f "%COMPOSE_FILE%" ps -q 2^>nul') do (
    set /a SERVICE_COUNT+=1
    set "STATUS="
    for /f "tokens=*" %%h in ('docker inspect --format="{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" %%c 2^>nul') do set "STATUS=%%h"
    for /f "tokens=*" %%n in ('docker inspect --format="{{.Name}}" %%c 2^>nul') do set "CNAME=%%n"
    if not defined STATUS (
        echo [FAIL] Could not inspect container %%c. Is it still being created?
        exit /b 1
    )
    if not "!STATUS!"=="healthy" if not "!STATUS!"=="running" (
        echo [FAIL] Container !CNAME! is in state: !STATUS!
        exit /b 1
    )
    echo [PASS] Container !CNAME! is !STATUS!.
)
if !SERVICE_COUNT!==0 (
    echo [FAIL] No Docker containers are running. Run scripts\start-docker.bat first.
    exit /b 1
)
exit /b 0

REM ========================================
:check_dotnet
REM Verify .NET 8+ SDK is available (newer SDKs can build net8.0 projects)
echo [CHECK] Checking .NET SDK...
for /f "tokens=*" %%i in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%i
if "%DOTNET_VERSION%"=="" (
    echo [FAIL] .NET SDK is not installed or not found in PATH.
    echo [INFO] Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)
REM Extract major version and require >= 8 (newer SDKs are backward compatible)
for /f "tokens=1 delims=." %%v in ("%DOTNET_VERSION%") do set DOTNET_MAJOR=%%v
if "%DOTNET_MAJOR%"=="" set DOTNET_MAJOR=0
if !DOTNET_MAJOR! LSS 8 (
    echo [FAIL] .NET SDK version %DOTNET_VERSION% found, but version 8 or later is required.
    echo [INFO] Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)
echo [PASS] .NET SDK version %DOTNET_VERSION% found.
exit /b 0

REM ========================================
:check_node
REM Verify Node.js v18+ is available
echo [CHECK] Checking Node.js...
for /f "tokens=*" %%i in ('node --version 2^>nul') do set NODE_VERSION=%%i
if "%NODE_VERSION%"=="" (
    echo [FAIL] Node.js is not installed or not found in PATH.
    echo [INFO] Install Node.js v18 or later from https://nodejs.org/
    exit /b 1
)
REM Extract major version (handle formats: v18.17.1, v20.0.0, etc.)
for /f "tokens=1 delims=v." %%v in ("!NODE_VERSION!") do set NODE_MAJOR=%%v
if "%NODE_MAJOR%"=="" set NODE_MAJOR=0
if !NODE_MAJOR! LSS 18 (
    echo [FAIL] Node.js version %NODE_VERSION% found, but v18 or later is required.
    echo [INFO] Install Node.js v18 or later from https://nodejs.org/
    exit /b 1
)
echo [PASS] Node.js version %NODE_VERSION% found.
exit /b 0

REM ========================================
:check_port
REM Verify a given port is free
set PORT=%2
if "%PORT%"=="" (
    echo [ERROR] No port specified. Usage: check_port ^<PORT^>
    exit /b 1
)
echo [CHECK] Checking if port %PORT% is available...
REM Use findstr to check only LISTENING entries to avoid false positives from TIME_WAIT connections
netstat -ano | findstr /R /C:"[:\.]%PORT%[^0-9]" | findstr /C:"LISTENING" >nul 2>&1
if not errorlevel 1 (
    echo [FAIL] Port %PORT% is already in use.
    echo [INFO] Please stop the process using port %PORT% or change the configuration.
    echo [INFO] To find the process: netstat -ano ^| findstr :%PORT%
    exit /b 1
)
echo [PASS] Port %PORT% is available.
exit /b 0

REM ========================================
:check_file
REM Verify a file exists
REM Strip quotes from argument in case caller wrapped the path in quotes
set CHECK_FILE=%~2
if "%CHECK_FILE%"=="" (
    echo [ERROR] No file specified. Usage: check_file ^<PATH^>
    exit /b 1
)  

echo [CHECK] Checking for %CHECK_FILE%...
if not exist "%CHECK_FILE%" (
    echo [FAIL] Required file not found: %CHECK_FILE%
    exit /b 1
)
echo [PASS] Found %CHECK_FILE%.
exit /b 0

REM ========================================
:check_adb
REM Verify ADB is available
echo [CHECK] Checking Android Debug Bridge (ADB)...
adb --version >nul 2>&1
if errorlevel 1 (
    echo [FAIL] ADB is not installed or not found in PATH.
    echo [INFO] Install Android SDK Platform Tools from https://developer.android.com/studio/releases/platform-tools
    exit /b 1
)
REM "adb --version" line looks like: "Android Debug Bridge version 1.0.41"
REM tokens 1-2 were "Android"/"Debug", so %%j (token 2) printed "Debug" instead
REM of the version. The version is token 4.
for /f "tokens=4 delims= " %%i in ('adb --version 2^>nul ^| findstr /i "version"') do set ADB_VERSION=%%i
if not defined ADB_VERSION set ADB_VERSION=unknown
echo [PASS] ADB version %ADB_VERSION% found.
exit /b 0

REM ========================================
:check_adb_device
REM Verify at least one Android device is connected and authorized
echo [CHECK] Checking for connected Android devices...
adb devices >nul 2>&1
if errorlevel 1 (
    echo [FAIL] ADB is not available. Run check_adb first.
    exit /b 1
)
set DEVICE_FOUND=0
set UNAUTHORIZED=0
for /f "skip=1 tokens=1,2" %%a in ('adb devices') do (
    if "%%b"=="device" (
        set DEVICE_FOUND=1
    )
    if "%%b"=="unauthorized" (
        set UNAUTHORIZED=1
    )
)
if %UNAUTHORIZED%==1 (
    echo [FAIL] Android device detected but not authorized.
    echo [INFO] Check the device screen for an "Allow USB debugging?" popup and accept it.
    echo [INFO] After accepting, run 'adb devices' to verify the device is listed as "device".
    exit /b 1
)
if %DEVICE_FOUND%==0 (
    echo [FAIL] No Android device connected.
    echo [INFO] Connect a device via USB with USB debugging enabled.
    echo [INFO] Run 'adb devices' to verify the device is listed as "device".
    echo [INFO] Check for authorization popup on the device screen.
    exit /b 1
)
echo [PASS] At least one Android device is connected.
exit /b 0

REM ========================================
:check_api_healthy
REM Poll http://localhost:5049/health until ready or timeout
echo [CHECK] Waiting for API at http://localhost:5049/health...
set RETRIES=0
REM 90 retries at ~1s each = ~90s. The previous 30s timeout was too short for
REM cold starts that include EF Core migrations or a first-time dotnet build.
set MAX_RETRIES=90
set LAST_STATUS=

:health_loop
if !RETRIES! geq %MAX_RETRIES% (
    echo [FAIL] API did not become healthy within %MAX_RETRIES% seconds.
    if defined LAST_STATUS (
        echo [INFO] Last HTTP status seen: !LAST_STATUS!
    ) else (
        echo [INFO] Could not connect at all - the API process likely never started or is still building.
    )
    echo [INFO] Check the "Midi-Kaval API" terminal window for build errors, migration errors, or port-binding issues.
    echo [INFO] Confirm the API actually exposes a /health endpoint and is bound to port 5049.
    exit /b 1
)

REM Use curl if available, fall back to PowerShell
where curl >nul 2>&1
if not errorlevel 1 (
    REM Write status code to a temp file to avoid pipe parsing issues in parenthesized blocks
    curl -s -o nul -w "%%{http_code}" http://localhost:5049/health >"%TEMP%\mk_health_check.txt" 2>&1
    set /p HTTP_STATUS=<"%TEMP%\mk_health_check.txt"
    if defined HTTP_STATUS set LAST_STATUS=!HTTP_STATUS!
    if not "!HTTP_STATUS!"=="200" (
        set /a RETRIES+=1
        timeout /t 1 >nul
        goto :health_loop
    )
) else (
    powershell -Command "try { $r = Invoke-WebRequest -Uri 'http://localhost:5049/health' -UseBasicParsing -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } else { exit 1 } } catch { exit 1 }" >nul 2>&1
    if errorlevel 1 (
        set /a RETRIES+=1
        timeout /t 1 >nul
        goto :health_loop
    )
)
echo [PASS] API is healthy at http://localhost:5049/health.
exit /b 0

endlocal

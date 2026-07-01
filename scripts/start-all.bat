@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Start All Services
echo ========================================
echo.

REM --- Parse command-line arguments ---
set START_WEB=1
set START_MOBILE=0
set HAS_FLAGS=0

:parse_args
if "%1"=="" goto :args_done
set HAS_FLAGS=1
if /i "%1"=="--no-web" (
    set START_WEB=0
) else if /i "%1"=="--web-only" (
    set START_MOBILE=0
    set START_WEB=1
) else if /i "%1"=="--mobile" (
    set START_MOBILE=1
) else if /i "%1"=="--all" (
    set START_WEB=1
    set START_MOBILE=1
) else (
    echo [WARN] Unknown flag: %1
    echo [INFO] Supported flags: --no-web, --web-only, --mobile, --all
)
shift
goto :parse_args
:args_done

REM If no flags specified, prompt user for mobile
if "%HAS_FLAGS%"=="0" (
    echo [INFO] Do you want to start the mobile app as well? (Y/N)
    set /p START_MOBILE_CHOICE=
    if /i "!START_MOBILE_CHOICE!"=="Y" set START_MOBILE=1
    if /i "!START_MOBILE_CHOICE!"=="YES" set START_MOBILE=1
)

REM ========================================
REM Step 1: Start Docker infrastructure
REM ========================================
echo.
echo ========================================
echo  Step 1/3: Docker Infrastructure
echo ========================================
call "%~dp0start-docker.bat"
if errorlevel 1 (
    echo [ERROR] Docker infrastructure failed to start. Aborting.
    exit /b 1
)
echo [PASS] Docker infrastructure is running.

REM ========================================
REM Step 2: Start API
REM ========================================
echo.
echo ========================================
echo  Step 2/3: API
echo ========================================
echo [INFO] Opening API in a new terminal window...
start "Midi-Kaval API" cmd /c "%~dp0start-api.bat"

REM Brief pause to let the API start initializing
timeout /t 3 >nul

echo [INFO] Waiting for API to become healthy...
call "%~dp0_check-prereqs.bat" check_api_healthy
if errorlevel 1 (
    echo [ERROR] API failed to start within the timeout.
    echo [INFO] Check the "Midi-Kaval API" terminal window for errors.
    exit /b 1
)
echo [PASS] API is running at http://localhost:5049/swagger.

REM ========================================
REM Step 3: Start Web and/or Mobile
REM ========================================
echo.
echo ========================================
echo  Step 3/3: Frontend
echo ========================================

if "%START_WEB%"=="1" (
    echo [INFO] Opening Web app in a new terminal window...
    start "Midi-Kaval Web" cmd /c "%~dp0start-web.bat"
    echo [INFO] Web app starting at http://localhost:4200
)

if "%START_MOBILE%"=="1" (
    echo [INFO] Starting Mobile app...
    call "%~dp0start-mobile.bat"
    if errorlevel 1 (
        echo [WARN] Mobile app failed to start. Continuing with other services.
    )
)

echo.
echo ========================================
echo  All requested services started!
echo ========================================
echo.
echo  Running Services:
echo    - Docker Infrastructure  (terminal: background)
echo    - API                    (terminal: Midi-Kaval API)
if "%START_WEB%"=="1" echo    - Web Frontend          (terminal: Midi-Kaval Web)
if "%START_MOBILE%"=="1" echo    - Mobile App            (connected Android device)
echo.
echo  Service URLs:
echo    - API:     http://localhost:5049/swagger
if "%START_WEB%"=="1" echo    - Web:     http://localhost:4200
if "%START_MOBILE%"=="1" echo    - Metro:   http://localhost:8081
echo.
echo  To stop all services, run: scripts\stop-all.bat

endlocal

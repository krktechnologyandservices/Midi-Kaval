@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Start Web Frontend
echo ========================================
echo.

REM --- Prerequisite: Docker (infrastructure) ---
call "%~dp0_check-prereqs.bat" check_docker
if errorlevel 1 (
    echo [ERROR] Docker is not running. The API requires PostgreSQL, Redis, and Azurite.
    echo [INFO] Run scripts\start-docker.bat first to start the infrastructure, then try again.
    exit /b 1
)

REM --- Prerequisite: Node.js ---
call "%~dp0_check-prereqs.bat" check_node
if errorlevel 1 (
    echo [ERROR] Node.js v18+ is required to run the web frontend.
    exit /b 1
)

REM --- Prerequisite: Port 4200 ---
call "%~dp0_check-prereqs.bat" check_port 4200
if errorlevel 1 (
    echo [ERROR] Port 4200 is already in use. The Angular dev server cannot start.
    echo [INFO] Stop the process using port 4200 or change the dev server port.
    exit /b 1
)

REM --- Check node_modules ---
set WEB_DIR=%~dp0..\apps\web
if not exist "%WEB_DIR%" (
    echo [ERROR] Web project not found at %WEB_DIR%
    exit /b 1
)

if not exist "%WEB_DIR%\node_modules" (
    echo [INFO] node_modules not found. Running npm install...
    cd /d "%WEB_DIR%"
    npm install
    if errorlevel 1 (
        echo [ERROR] npm install failed.
        exit /b 1
    )
    echo [INFO] npm install completed.
) else (
    echo [PASS] node_modules found in apps/web.
)

REM --- Build shared-types if needed ---
set ROOT_DIR=%~dp0..
if exist "%ROOT_DIR%\packages\shared-types" (
    if not exist "%ROOT_DIR%\packages\shared-types\dist" (
        echo [INFO] Building shared-types...
        cd /d "%ROOT_DIR%"
        npm run build:shared-types
        if errorlevel 1 (
            echo [ERROR] Failed to build shared-types.
            echo [INFO] Verify that the 'build:shared-types' script exists in root package.json.
            exit /b 1
        )
        echo [INFO] shared-types built successfully.
    ) else (
        echo [PASS] shared-types already built.
    )
)

echo.
echo [INFO] Starting Angular dev server at http://localhost:4200...
echo [INFO] The browser will open automatically.
echo [INFO] Press Ctrl+C to stop the web server.
echo.
cd /d "%WEB_DIR%"
npx ng serve --open

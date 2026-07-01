@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - Start Mobile App
echo ========================================
echo.

REM --- Prerequisite: ADB ---
call "%~dp0_check-prereqs.bat" check_adb
if errorlevel 1 (
    echo [ERROR] ADB is required to run the mobile app on a connected device.
    exit /b 1
)

REM --- Prerequisite: Android device connected ---
call "%~dp0_check-prereqs.bat" check_adb_device
if errorlevel 1 (
    echo [ERROR] No Android device is connected.
    echo [INFO] Connect a device via USB with USB debugging enabled.
    exit /b 1
)

REM --- Prerequisite: Port 8081 (Metro) ---
call "%~dp0_check-prereqs.bat" check_port 8081
if errorlevel 1 (
    echo [ERROR] Port 8081 is already in use. The Metro bundler cannot start.
    echo [INFO] Stop the process using port 8081.
    exit /b 1
)

REM --- Wait for API to be healthy ---
call "%~dp0_check-prereqs.bat" check_api_healthy
if errorlevel 1 (
    echo [ERROR] API is not reachable. The mobile app requires the API.
    echo [INFO] Ensure the API is running (start-api.bat) before starting the mobile app.
    exit /b 1
)

REM --- Set up ADB reverse proxy for API access ---
echo [INFO] Setting up ADB reverse proxy for API (port 5049)...
adb reverse tcp:5049 tcp:5049
if errorlevel 1 (
    echo [WARN] Failed to set up ADB reverse proxy for port 5049.
    echo [INFO] You may need to manually run: adb reverse tcp:5049 tcp:5049
) else (
    echo [PASS] ADB reverse proxy configured: device:5049 -^> host:5049.
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
            exit /b 1
        )
        echo [INFO] shared-types built successfully.
    ) else (
        echo [PASS] shared-types already built.
    )
)

REM --- Verify mobile project exists ---
set MOBILE_DIR=%~dp0..\apps\mobile
if not exist "%MOBILE_DIR%" (
    echo [ERROR] Mobile project not found at %MOBILE_DIR%
    exit /b 1
)

echo.
echo [INFO] Starting Metro bundler in background...
cd /d "%MOBILE_DIR%"

REM Check that npx and react-native are available before launching
where npx >nul 2>&1
if errorlevel 1 (
    echo [ERROR] npx not found. Ensure Node.js is installed and in PATH.
    exit /b 1
)

REM Start Metro bundler in background (note: --projectRoot, not --project)
start "Metro Bundler" cmd /c "npx react-native start --projectRoot %MOBILE_DIR%"

REM Brief wait for Metro window to appear
timeout /t 2 >nul

REM Wait for Metro bundler to be ready on port 8081
echo [INFO] Waiting for Metro bundler to start...
set RETRIES=0
set MAX_RETRIES=30

:metro_loop
if !RETRIES! geq %MAX_RETRIES% (
    echo [ERROR] Metro bundler did not start within %MAX_RETRIES% seconds.
    echo [INFO] Check the Metro bundler window for errors (e.g., missing dependencies).
    echo [INFO] Try running 'npx react-native start' manually from apps\mobile to diagnose.
    exit /b 1
)

REM Use curl if available, fall back to PowerShell
where curl >nul 2>&1
if not errorlevel 1 (
    curl -s -o nul http://localhost:8081 2>nul
) else (
    powershell -Command "try { Invoke-WebRequest -Uri 'http://localhost:8081' -UseBasicParsing -TimeoutSec 2 | Out-Null; exit 0 } catch { exit 1 }" >nul 2>&1
)
if errorlevel 1 (
    set /a RETRIES+=1
    timeout /t 1 >nul
    goto :metro_loop
)
echo [PASS] Metro bundler is ready at http://localhost:8081.

echo.
echo [INFO] Installing and launching the app on the connected Android device...
npx react-native run-android --projectRoot "%MOBILE_DIR%"
if errorlevel 1 (
    echo [ERROR] Failed to install or launch the app on the Android device.
    echo [INFO] Check the device connection and try again.
    exit /b 1
)

echo.
echo ========================================
echo  Mobile app launched successfully!
echo ========================================
echo [INFO] The app is running on the connected Android device.
echo [INFO] Metro bundler is running in a separate window.
echo [INFO] To stop all services, run scripts\stop-all.bat.

endlocal

# Midi-Kaval Local Development Setup Scripts

This directory contains Windows Batch (`.bat`) scripts for quickly starting and stopping the Midi-Kaval development environment.

## Prerequisites

Before running these scripts, ensure the following are installed:

| Tool | Version | Required For | Download |
|------|---------|--------------|----------|
| Docker Desktop | Latest | PostgreSQL, Redis, Azurite | [docker.com](https://www.docker.com/products/docker-desktop/) |
| .NET SDK | 8.x | API | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Node.js | 18+ | Web + Mobile | [nodejs.org](https://nodejs.org/) |
| Android SDK | Latest | Mobile | [developer.android.com](https://developer.android.com/studio) |
| ADB | Latest | Mobile (USB deploy) | Part of Android SDK Platform Tools |

## Scripts Overview

### Infrastructure

| Script | What it does |
|--------|--------------|
| `start-docker.bat` | Starts PostgreSQL 16, Redis 7, and Azurite via Docker Compose. Waits for health checks before exiting. |

### Application

| Script | What it does |
|--------|--------------|
| `start-api.bat` | Verifies Docker is running, .NET SDK is available, port 5049 is free, then starts the API via `dotnet run`. |
| `start-web.bat` | Verifies Node.js is available, port 4200 is free, installs `node_modules` if needed, builds shared-types, then starts the Angular dev server. |
| `start-mobile.bat` | Verifies ADB is available, an Android device is connected, port 8081 is free, API is healthy, sets up ADB reverse proxies, starts Metro bundler, then installs and launches the app on the device. |

### Convenience

| Script | What it does |
|--------|--------------|
| `start-all.bat` | Sequentially starts Docker, API, Web (and optionally Mobile). Opens services in separate terminal windows. Supports `--no-web`, `--web-only`, `--mobile`, `--all` flags. |
| `stop-all.bat` | Kills API, Angular, and Metro processes, runs `docker compose down`, and optionally removes volumes (-v). |

### Shared Helper

| Script | What it does |
|--------|--------------|
| `_check-prereqs.bat` | Internal helper called by other scripts for prerequisite checks. Not intended to be run directly. |

## Startup Workflow

The recommended startup order is:

```
start-docker.bat  →  start-api.bat  →  start-web.bat  /  start-mobile.bat
```

Or use the convenience script:

```
start-all.bat           # Starts Docker + API + Web (prompts for Mobile)
start-all.bat --all     # Starts all services including Mobile
start-all.bat --mobile  # Starts Docker + API + Mobile (no Web)
```

## Stopping Everything

```
stop-all.bat
```

This kills all running processes (API, Angular, Metro) and stops Docker containers. You'll be prompted about whether to remove Docker volumes (which deletes database data).

## Troubleshooting

### Docker Desktop not running

```
[FAIL] Docker Desktop is not running or not installed.
[INFO] Please start Docker Desktop and try again.
```

**Fix:** Start Docker Desktop from the Start Menu or system tray. Wait for the Docker icon to stop animating (it should be steady, indicating the daemon is ready).

### Port already in use

```
[FAIL] Port 5049 is already in use.
[INFO] Please stop the process using port 5049 or change the configuration.
```

**Fix:** Identify the process using the port:
```
netstat -ano | findstr :5049
tasklist /FI "PID eq <PID>"
```
Then either stop the process via Task Manager or change the port configuration.

### ADB device not authorized

```
[FAIL] No Android device connected.
[INFO] Check for authorization popup on the device screen.
```

**Fix:** 
1. Connect the device via USB
2. Enable USB debugging on the device (Developer Options)
3. Check the device screen for an "Allow USB debugging?" popup — accept it
4. Run `adb devices` to verify the device is listed as "device" (not "unauthorized")

### API fails to start (DB connection)

If the API fails to connect to the database, ensure:
1. Docker containers are running (`docker compose -f infra/docker-compose.yml ps`)
2. The connection string in `apps/api/appsettings.Development.json` matches the Docker Compose configuration
3. Database migrations have been applied. Run:
   ```
   cd apps/api
   dotnet ef database update
   ```

### npm / node_modules issues

If the web or mobile apps fail to start due to missing or corrupted packages:
1. Delete `node_modules` in the affected project
2. Run `npm install` from the project directory
3. Retry the script

## Notes

- The API uses `appsettings.Development.json` which is pre-configured for local Docker services (PostgreSQL on localhost:5432, Redis on localhost:6379, Azurite on localhost:10000).
- The mobile app connects to the API via ADB reverse proxy (`adb reverse tcp:5049 tcp:5049`), forwarding the device's localhost to the host machine.
- No Dockerfiles for API or Web are included yet — the API and Web run natively on the developer's machine. Docker is used only for infrastructure services.
- These scripts are Windows-only. Linux and macOS support is planned for a future update.

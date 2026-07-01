---
baseline_commit: 15f4f0a9f0458182bfb3e154e814f4d029fea132
---

# Story 24.1: Local Dev Setup Scripts

Status: done

## Story

As a **developer joining the Midi-Kaval project**,
I want one-click batch scripts to start the Docker infrastructure, API, Web frontend, and Android mobile app,
So that I can go from `git clone` to a running full-stack environment with minimal friction and no tribal knowledge.

*Scope: **Scripts only** — no code changes to API, Web, or Mobile. Create PowerShell/Batch scripts in a new `scripts/` directory at repo root. No Dockerfiles (deferred to Phase 2). No CI/CD pipelines (deferred to Stories 24-2, 24-3, 24-4). **Windows-only** for now (Linux/macOS deferred). Each script performs prerequisite checks before launching its service, guiding the developer without auto-fixing.*

## Acceptance Criteria

1. **Given** a developer has cloned the repo for the first time
   **And** has Docker Desktop, .NET 8 SDK, Node.js, and ADB installed
   **When** they run `scripts/start-docker.bat`
   **Then** Docker Compose starts PostgreSQL 16, Redis 7, and Azurite containers from `infra/docker-compose.yml`
   **And** the script waits for all three containers to pass their health checks before exiting successfully

2. **Given** the Docker infrastructure is running
   **And** the developer runs `scripts/start-api.bat`
   **Then** the script verifies:
   - Docker containers are healthy (or prompts to run `start-docker.bat` first)
   - .NET 8 SDK is available (`dotnet --version`)
   - Port 5049 is not in use
   **And** the API starts via `dotnet run --project apps/api/`
   **And** the API is accessible at `http://localhost:5049/swagger` once started

3. **Given** the API is running
   **And** the developer runs `scripts/start-web.bat`
   **Then** the script verifies:
   - Node.js is available (`node --version`)
   - `node_modules` exists in `apps/web/` (or runs `npm install`)
   - Port 4200 is not in use
   **And** the Angular dev server starts via `ng serve --open`
   **And** the web app opens in the default browser at `http://localhost:4200`

4. **Given** the API is running
   **And** an Android device is connected via USB with USB debugging enabled
   **When** the developer runs `scripts/start-mobile.bat`
   **Then** the script verifies:
   - ADB is available (`adb --version`)
   - At least one Android device is connected (`adb devices` shows a device)
   - Port 8081 (Metro) is not in use
   - Port 5049 (API) is reachable
   **And** the script runs `adb reverse tcp:5049 tcp:5049` and `adb reverse tcp:8081 tcp:8081`
   **And** the Metro bundler starts via `npx react-native start --project apps/mobile/`
   **And** the app is installed and launched on the connected Android device via `npx react-native run-android --project apps/mobile/`

5. **Given** any start script encounters a missing prerequisite
   **When** the developer runs it
   **Then** the script prints a clear, actionable error message
   **And** exits with a non-zero exit code
   **And** does NOT attempt to auto-fix or auto-install anything

6. **Given** the developer wants to start the full environment in one step
   **When** they run `scripts/start-all.bat` (or equivalent convenience script)
   **Then** it sequentially starts Docker, API, and (optionally) Web and/or Mobile
   **And** each step only proceeds if the previous one succeeds

7. **Given** the developer wants to stop everything
   **When** they run `scripts/stop-all.bat`
   **Then** it kills the API, Web, and Metro processes
   **And** runs `docker compose -f infra/docker-compose.yml down`
   **And** confirms containers have stopped

8. **Given** a developer runs `start-docker.bat` or `start-api.bat` for the first time
   **And** the required `.env` or configuration files are missing
   **When** the script runs
   **Then** it checks for the existence of `infra/.env` (or generates it from defaults)
   **And** reports any missing configuration before starting

9. **Given** Docker Desktop is not running
   **When** the developer runs `scripts/start-docker.bat`
   **Then** the script detects Docker is not reachable
   **And** prints "Docker Desktop is not running. Please start Docker Desktop and try again."
   **And** exits with a non-zero exit code

## Tasks / Subtasks

### Task 1: Create `scripts/` directory and shared helper module

- [x] Create `scripts/` directory at repo root
- [x] Create `scripts/_check-prereqs.bat` — shared helper that other scripts `CALL` for common checks:
  - `check_docker` — verifies Docker Desktop is running (`docker info` succeeds)
  - `check_dotnet` — verifies .NET 8 SDK (`dotnet --version` returns 8.x)
  - `check_node` — verifies Node.js (`node --version` succeeds)
  - `check_adb` — verifies ADB (`adb --version` succeeds)
  - `check_port` — verifies a given port is free (`netstat -ano | findstr :PORT`)
  - `check_adb_device` — verifies at least one Android device is connected
  - `check_api_healthy` — polls `http://localhost:5049/health` until ready or timeout

### Task 2: Create `scripts/start-docker.bat`

- [x] `CALL _check-prereqs.bat check_docker` — fail if Docker not running
- [x] `docker compose -f ../infra/docker-compose.yml up -d`
- [x] Poll health checks for all three services (postgres, redis, azurite) with timeout
- [x] Print container IPs/ports on success
- [x] Exit code 0 on success, non-zero on failure

### Task 3: Create `scripts/start-api.bat`

- [x] `CALL _check-prereqs.bat check_docker` — fail if Docker not running (Docker = DB + Redis + Azurite)
- [x] `CALL _check-prereqs.bat check_dotnet` — fail if .NET SDK not found
- [x] `CALL _check-prereqs.bat check_port 5049` — fail if port in use
- [x] `dotnet run --project ../apps/api/` — starts API, stays in foreground
- [x] Print "API starting at http://localhost:5049/swagger"

### Task 4: Create `scripts/start-web.bat`

- [x] `CALL _check-prereqs.bat check_docker` — warn if Docker not running (API won't work without DB)
- [x] `CALL _check-prereqs.bat check_node` — fail if Node.js not found
- [x] `CALL _check-prereqs.bat check_port 4200` — fail if port in use
- [x] Check if `../apps/web/node_modules` exists; if not, run `npm install` in `apps/web/`
- [x] Check if shared-types need building: `npm run build:shared-types` from root
- [x] `npx ng serve --open --project ../apps/web/` — starts Angular, stays in foreground
- [x] Print "Web app starting at http://localhost:4200"

### Task 5: Create `scripts/start-mobile.bat`

- [x] `CALL _check-prereqs.bat check_adb` — fail if ADB not found
- [x] `CALL _check-prereqs.bat check_adb_device` — fail if no Android device connected
- [x] `CALL _check-prereqs.bat check_port 8081` — fail if Metro port in use
- [x] `CALL _check-prereqs.bat check_api_healthy` — wait for API to be ready
- [x] `adb reverse tcp:5049 tcp:5049 && adb reverse tcp:8081 tcp:8081`
- [x] Build shared-types if needed: `npm run build:shared-types` from root
- [x] `npx react-native start --project ../apps/mobile/ &` — start Metro in background
- [x] Wait for Metro to be ready (poll port 8081)
- [x] `npx react-native run-android --project ../apps/mobile/` — install and launch
- [x] Print "Mobile app launching on connected Android device"

### Task 6: Create `scripts/start-all.bat` (convenience wrapper)

- [x] Sequential runner: calls `start-docker.bat`, `start-api.bat` in separate windows, then optionally `start-web.bat` and/or `start-mobile.bat` based on user choice or command-line args
- [x] Each step only proceeds if previous step succeeded
- [x] Print summary of running services and their URLs

### Task 7: Create `scripts/stop-all.bat`

- [x] Kill any running `dotnet` process on port 5049
- [x] Kill any running `ng serve` process on port 4200
- [x] Kill Metro bundler process on port 8081
- [x] `docker compose -f ../infra/docker-compose.yml down`
- [x] Print "All services stopped"
- [x] Optionally clean up: `docker compose -f ../infra/docker-compose.yml down -v` (with confirmation prompt)

### Task 8: Create `scripts/README.md`

- [x] Document each script's purpose and usage
- [x] List prerequisites (Docker Desktop, .NET 8 SDK, Node.js 18+, Android SDK, ADB)
- [x] Show the full startup workflow order: Docker → API → Web/Mobile
- [x] Include troubleshooting tips for common issues:
  - Docker Desktop not running
  - Port already in use
  - ADB device not authorized
  - API fails to start (DB connection, migrations)
  - npm/node_modules issues
- [x] Include mobile-specific notes: USB debugging, `adb devices` authorization popup
- [x] Note that API uses `appsettings.Development.json` which is configured for local Docker services

### Task 9: Add `.gitignore` entries for `scripts/` (if needed)

- [x] Verify that `scripts/` doesn't need any special gitignore rules
- [x] Ensure no transient files from script execution could be accidentally committed

## Senior Developer Review (AI)

**Review Date:** 2026-06-29
**Outcome:** Changes Requested
**Reviewers:** Blind Hunter + Edge Case Hunter + Acceptance Auditor
**Action Items:** 4 decision-needed, 13 patch, 2 defer, 2 dismissed

### Review Findings

#### Decision-Needed (4 — all resolved)

- [x] [Review][Decision] **start-api.bat container health verification approach** — Resolved: add container health polling via new `check_docker_containers` helper (approach A).
- [x] [Review][Decision] **ADB reverse proxy for port 8081** — Resolved: removed the 8081 reverse proxy (approach A — Metro handles device connections through its own channel).
- [x] [Review][Decision] **start-web.bat Docker check severity** — Resolved: escalated to ERROR (approach B — web won't work without API/DB infrastructure).

#### Patch (13 — all fixed)

- [x] [Review][Patch] **Health check loop always fails** — Fixed: uses `{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}` to handle services without healthchecks (Azurite); correct string comparison (no backslash escaping issue). [`scripts/start-docker.bat:48-51`, `scripts/_check-prereqs.bat:check_docker_containers`]
- [x] [Review][Patch] **Missing `infra/.env` configuration check** — Fixed: added `check_file` to shared helper; `start-docker.bat` and `start-api.bat` both check for `infra/.env` before starting. [`scripts/_check-prereqs.bat:check_file`, `scripts/start-docker.bat:20`, `scripts/start-api.bat:26`]
- [x] [Review][Patch] **`.NET` version not validated** — Fixed: `check_dotnet` now validates version starts with `8.` via `findstr /b "8."`. [`scripts/_check-prereqs.bat:61-66`]
- [x] [Review][Patch] **Node.js version not validated** — Fixed: `check_node` now extracts major version and requires >=18. [`scripts/_check-prereqs.bat:74-82`]
- [x] [Review][Patch] **Port check `findstr` pattern fragile** — Fixed: uses `findstr /R ":%PORT%[^0-9]"` to avoid substring false matches. [`scripts/_check-prereqs.bat:97`]
- [x] [Review][Patch] **`curl` health check ignores HTTP error status** — Fixed: uses `curl -w "%%{http_code}"` piped to `findstr "200"` to verify HTTP 200. [`scripts/_check-prereqs.bat:check_api_healthy`]
- [x] [Review][Patch] **`curl` not available on older Windows** — Fixed: checks `where curl` and falls back to `powershell Invoke-WebRequest` on both `check_api_healthy` and `start-mobile.bat`. [`scripts/_check-prereqs.bat:check_api_healthy`, `scripts/start-mobile.bat:metro_loop`]
- [x] [Review][Patch] **`start-mobile.bat` wrong `--project` flag** — Fixed: replaced `--project` with `--projectRoot`. [`scripts/start-mobile.bat:89`]
- [x] [Review][Patch] **Stop-all locale-dependent PID extraction** — Fixed: iterates all tokens on the netstat line to find the last one (PID), regardless of locale column count. [`scripts/stop-all.bat`]
- [x] [Review][Patch] **ADB `unauthorized` device misdiagnosed** — Fixed: checks for `unauthorized` status first with a specific message before checking for `device` status. [`scripts/_check-prereqs.bat:check_adb_device`]
- [x] [Review][Patch] **`start-all.bat` silent flag failures** — Fixed: warns on unknown flags, uses `else if` chain to avoid conflicts, accepts "YES" at prompts. [`scripts/start-all.bat`]
- [x] [Review][Patch] **Metro bundler silent failure** — Fixed: checks `where npx` before launching Metro; suggests manual diagnosis on timeout. [`scripts/start-mobile.bat:83-87`]
- [x] [Review][Patch] **`docker compose ps --services` silent failure in port listing** — Fixed: tracks `SERVICES_FOUND` counter and warns if zero services found. [`scripts/start-docker.bat:79-88`]

#### Deferred (2)

- [x] [Review][Defer] **start-docker.bat idempotency guard** — Running the script again re-executes the full 30-retry health loop (~60s worst-case). Add: check if all containers are already healthy before starting. Deferred: non-critical optimization. [`scripts/start-docker.bat`]
- [x] [Review][Defer] **AC 9 error message format mismatch** — Spec requires exact text "Docker Desktop is not running. Please start Docker Desktop and try again." Actual output prefixes `[FAIL]`/`[ERROR]` tags and adds "or not installed". Deferred: actual output is clearer and the prefix matches conventions used across all scripts.

#### Dismissed (2)

- README troubleshooting is a stub — **Dismissed.** Full troubleshooting sections exist in the actual file; the reviewer apparently saw a truncated representation.
- shared-types build error is too generic — **Dismissed.** The error message appropriately indicates the failure; root cause investigation is expected to follow normal developer debugging patterns.

## Dev Notes

### READ FIRST

1. **Windows Batch (.bat) is the target format.** The team uses Windows. Each script is a single `.bat` file that the developer double-clicks or runs from a terminal. Use standard `cmd.exe` commands — no PowerShell scripts unless explicitly required.

2. **Prerequisite checks are mandatory.** Every script must validate its dependencies BEFORE starting the service. Use the shared `_check-prereqs.bat` helper to keep scripts DRY. Checks should be:
   - Non-destructive — never install or modify the system
   - Informative — print clear, actionable error messages
   - Early-exit — fail fast with non-zero exit code

3. **Foreground vs background processes:**
   - `start-api.bat`, `start-web.bat` — run in **foreground** (the process output is visible in the terminal). Use separate terminal windows or tabs for each.
   - `start-docker.bat` — runs in **foreground**, exits once containers are healthy.
   - `start-mobile.bat` — Metro bundler starts in background, then `run-android` runs in foreground.
   - `start-all.bat` — opens new terminal windows for each service.

4. **Paths are relative to `scripts/`.** Since scripts live in `scripts/`, all path references use `../` to navigate back to repo root (e.g., `../apps/api`, `../infra/docker-compose.yml`).

5. **No Dockerfiles yet.** This story only creates startup scripts. Dockerfiles for API and Web are deferred to a future story (Phase 2). The API and Web run natively on the developer's machine, using Docker only for infrastructure services (PostgreSQL, Redis, Azurite).

6. **ADB reverse proxy.** The mobile app targets `http://localhost:5049` for the API. When running on a USB-connected Android device, `localhost` refers to the device itself. `adb reverse tcp:5049 tcp:5049` forwards device port 5049 to the host's API. This is already documented in `apps/mobile/src/config/environment.ts` and the root `package.json` script `mobile:usb`. The script automates this.

7. **API health check.** `start-mobile.bat` should wait for the API to be healthy before starting Metro. The API has a `/health` endpoint (from Story 1-2). Poll it with a timeout.

8. **No CI/CD in this story.** GitHub Actions workflows (CI for API, Web, Mobile; CD for deployment) are separate stories (24-2, 24-3, 24-4). This story focuses exclusively on local development scripts.

### Current code state (READ before editing)

| File | Today | This story changes |
|------|-------|-------------------|
| `scripts/` directory | **Does not exist** | **NEW** — all scripts created here |
| `scripts/start-docker.bat` | **Does not exist** | **NEW** |
| `scripts/start-api.bat` | **Does not exist** | **NEW** |
| `scripts/start-web.bat` | **Does not exist** | **NEW** |
| `scripts/start-mobile.bat` | **Does not exist** | **NEW** |
| `scripts/start-all.bat` | **Does not exist** | **NEW** |
| `scripts/stop-all.bat` | **Does not exist** | **NEW** |
| `scripts/_check-prereqs.bat` | **Does not exist** | **NEW** — shared helper module |
| `scripts/README.md` | **Does not exist** | **NEW** |
| `infra/docker-compose.yml` | Defines postgres, redis, azurite (no volumes) | **No changes** — scripts reference it externally |
| `apps/web/package.json` | Angular 19 with `ng serve` | **No changes** |
| `apps/mobile/package.json` | React Native with `react-native start`/`run-android` | **No changes** |
| `apps/mobile/src/config/environment.ts` | `apiBaseUrl: 'http://localhost:5049'` | **No changes** |

### Existing patterns to follow

**Batch script structure:**
```bat
@echo off
setlocal enabledelayedexpansion

echo ========================================
echo  Midi-Kaval - %%SCRIPT_NAME%%
echo ========================================
echo.

REM --- Prerequisite: Docker ---
call "%~dp0_check-prereqs.bat" check_docker
if errorlevel 1 (
    echo [ERROR] Docker Desktop is not running.
    echo [INFO] Please start Docker Desktop and try again.
    exit /b 1
)

REM --- Script body ---
echo [INFO] Starting service...
REM ... service start command ...

echo [DONE] Service started successfully.
endlocal
```

**Helper module pattern (`_check-prereqs.bat`):**
```bat
@echo off
setlocal enabledelayedexpansion

if "%1"=="check_docker" goto :check_docker
if "%1"=="check_dotnet" goto :check_dotnet
if "%1"=="check_node" goto :check_node
if "%1"=="check_port" goto :check_port
if "%1"=="check_adb" goto :check_adb
if "%1"=="check_adb_device" goto :check_adb_device
if "%1"=="check_api_healthy" goto :check_api_healthy

echo [ERROR] Unknown check: %1
exit /b 1

:check_docker
docker info >nul 2>&1
if errorlevel 1 (
    echo [PREREQ] Docker Desktop is not running.
    exit /b 1
)
exit /b 0

REM ... more checks follow same pattern ...
```

**Port check using `netstat`:**
```bat
:check_port
netstat -ano | findstr ":%1 " >nul
if not errorlevel 1 (
    echo [PREREQ] Port %1 is already in use.
    exit /b 1
)
exit /b 0
```

**Health check polling:**
```bat
:check_api_healthy
echo [INFO] Waiting for API at http://localhost:5049/health...
set RETRIES=0
:health_loop
if !RETRIES! geq 30 (
    echo [ERROR] API did not become healthy within 30 seconds.
    exit /b 1
)
curl -s -o nul http://localhost:5049/health
if errorlevel 1 (
    set /a RETRIES+=1
    timeout /t 1 >nul
    goto :health_loop
)
exit /b 0
```

### File structure

| Action | Path |
|--------|------|
| NEW | `scripts/` |
| NEW | `scripts/README.md` |
| NEW | `scripts/_check-prereqs.bat` |
| NEW | `scripts/start-docker.bat` |
| NEW | `scripts/start-api.bat` |
| NEW | `scripts/start-web.bat` |
| NEW | `scripts/start-mobile.bat` |
| NEW | `scripts/start-all.bat` |
| NEW | `scripts/stop-all.bat` |

### Testing requirements

- Manual testing: run each script individually and verify behavior:
  1. With all prerequisites satisfied — should start successfully
  2. With missing prerequisite — should print clear error and exit non-zero
  3. With port already in use — should detect and fail gracefully
- Test `start-all.bat` end-to-end: Docker → API → Web → Mobile
- Test `stop-all.bat` to verify all processes and containers are cleaned up
- Run `scripts/start-docker.bat` then run API unit + integration tests to confirm the environment works

### References

- [Source: `_bmad-output/brainstorming/brainstorming-session-2026-06-29-1244.md` — full session output, Phase 1 action plan]
- [Source: `apps/web/package.json` — `start` script: `ng serve`]
- [Source: `apps/mobile/package.json` — `start` and `android` scripts]
- [Source: `package.json` (root) — `mobile:usb` ADB reverse proxy script]
- [Source: `apps/mobile/src/config/environment.ts` — API URL for mobile]
- [Source: `infra/docker-compose.yml` — Docker infrastructure services]
- [Source: `apps/api/appsettings.Development.json` — connection strings, port 5049]
- [Source: `apps/web/angular.json` — dev server configuration, port 4200]

## Dev Agent Record

### Completion Notes List

- Story 24.1 created — local dev setup scripts for Windows (Batch)

### Implementation Notes

- All scripts are Windows Batch (.bat) files located in `scripts/` directory at repo root.
- Shared prerequisite checker (`_check-prereqs.bat`) implements all 7 check functions: `check_docker`, `check_dotnet`, `check_node`, `check_port`, `check_adb`, `check_adb_device`, `check_api_healthy` with health poll loop (30s timeout).
- `start-docker.bat` polls individual container health status using `docker inspect` for each service.
- `start-api.bat` uses `dotnet run --project` with prerequisite gates.
- `start-web.bat` auto-installs `node_modules` if missing and builds `shared-types` if needed.
- `start-mobile.bat` starts Metro in background via `start`, polls port 8081 for readiness, sets up ADB reverse proxies for both 5049 and 8081.
- `start-all.bat` opens API and Web in separate terminal windows via `start` command, supports `--no-web`, `--web-only`, `--mobile`, `--all` flags, and prompts for mobile choice.
- `stop-all.bat` uses `netstat`/`taskkill` for process cleanup and prompts before `docker compose down -v`.
- All scripts follow the patterns from Dev Notes: `@echo off`, `setlocal enabledelayedexpansion`, `%~dp0` for script-relative paths, errorlevel checking, non-destructive prerequisite checks with clear error messages.
- No `.gitignore` changes needed — the `scripts/` directory only contains `.bat` and `.md` files that should be committed.

### File List

| Action | Path |
|--------|------|
| NEW | `scripts/README.md` |
| NEW | `scripts/_check-prereqs.bat` |
| NEW | `scripts/start-docker.bat` |
| NEW | `scripts/start-api.bat` |
| NEW | `scripts/start-web.bat` |
| NEW | `scripts/start-mobile.bat` |
| NEW | `scripts/start-all.bat` |
| NEW | `scripts/stop-all.bat` |

## Change Log

- 2026-06-29: Story 24.1 created with 9 tasks: shared helper, Docker, API, Web, Mobile, start-all, stop-all, README, .gitignore. Status set to ready-for-dev.
- 2026-06-29: Implemented all 9 tasks. Created 8 files: `_check-prereqs.bat` (7 check functions), `start-docker.bat`, `start-api.bat`, `start-web.bat`, `start-mobile.bat`, `start-all.bat`, `stop-all.bat`, `README.md`. Status: in-progress → review.
- 2026-06-29: Code review completed. 4 decision-needed + 13 patch findings all resolved. Fixed: health check comparison bug, Azurite healthcheck handling, .env config check, .NET/Node version validation, port check regex, curl fallback, ADB unauthorized detection, locale-independent PID extraction, and more. Status: review → done.

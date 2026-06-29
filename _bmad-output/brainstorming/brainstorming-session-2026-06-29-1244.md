---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Deployment scripts, local development setup, and DevOps pipelines'
session_goals: 'Plan how developers run/test locally (web + mobile), then production-grade DevOps pipelines for Docker and non-Docker deployments (Android & iOS)'
selected_approach: 'ai-recommended'
techniques_used: ['Mind Mapping', 'Reverse Brainstorming', 'How-Now-Wow Matrix']
ideas_generated: 15
session_active: false
workflow_completed: true
---

## Session Overview

**Topic:** Deployment scripts, local development setup, and DevOps pipelines
**Goals:** Plan how developers run/test locally (web + mobile), then production-grade DevOps pipelines for Docker and non-Docker deployments (Android & iOS)

### Project Context

Monorepo structure:
- **API** — .NET 8 ASP.NET Core (`apps/api/`)
- **Web** — Angular 19 PWA (`apps/web/`)
- **Mobile** — React Native 0.76 (`apps/mobile/`)
- **Infrastructure** — Docker Compose with PostgreSQL 16, Redis 7, Azurite (`infra/docker-compose.yml`)
- Production server: Windows Server with IIS
- CI/CD: GitHub Actions

### Session Setup

**Facilitator:** Admin
**Date:** 2026-06-29
**Approach:** AI-Recommended Techniques

## Technique Selection

**Approach:** AI-Recommended Techniques
**Techniques Used:** Mind Mapping, Reverse Brainstorming, How-Now-Wow Matrix

## Mind Mapping Results

### Local Development Scripts

| Script | What it does | Prerequisite checks |
|--------|-------------|-------------------|
| `start-docker.bat` | `docker compose up -d` from `infra/` | Docker Desktop running |
| `start-api.bat` | `dotnet run` for the API | Docker running, .NET 8 SDK, port 5049 free |
| `start-web.bat` | `ng serve --open` | Node.js, node_modules, port 4200 free |
| `start-mobile.bat` | `npx react-native start` + `run-android` | ADB device connected, `adb reverse`, port 8081 free |

### Developer Personas

- **Backend Dev** — needs API + Docker infra + tests
- **Frontend Web Dev** — needs API running + Angular dev server
- **Mobile Dev** — needs API running + USB-connected Android device via ADB
- **Full-Stack Dev** — needs all of the above
- **QA/Tester** — can use staging environment, not local dev

### Non-Docker Production Deployment (Windows Server + IIS)

| Layer | Technology | Deployment Method |
|-------|-----------|------------------|
| **API** | .NET 8 ASP.NET Core | `dotnet publish --self-contained -r win-x64` → IIS site via ASP.NET Core Module (ANCM). Config via environment variables set during deployment. |
| **Web** | Angular 19 | `ng build --configuration production` → static files in IIS site with URL Rewrite for SPA routing |
| **Database** | PostgreSQL 16 | Installed locally on Windows Server as a Windows service |
| **Cache** | Redis 7 | Installed locally on Windows Server as a Windows service |
| **Blob** | Azure Blob / SMB share | Configured via environment variables |
| **Android** | React Native | `./gradlew assembleRelease` → signed APK for distribution |

### Future: Docker Deployment (Phase 2)

- Dockerfile for API (.NET multi-stage build)
- Dockerfile for Web (Nginx serving Angular)
- Updated docker-compose.yml wiring all services
- Registry push + pull-to-deploy scripts
- Multi-environment Docker Compose profiles

### iOS (Deferred)

- Requires macOS / Xcode — cannot build on Windows
- Future options: GitHub Actions macos-latest runner, Expo/EAS Build, or dedicated Mac build machine

## How-Now-Wow Matrix

### NOW (Build First — Immediate Action)

1. **start-docker.bat** — `docker compose up -d` with health check wait
2. **start-api.bat** — Prerequisite checks (Docker, .NET SDK, port) + `dotnet run`
3. **start-web.bat** — Prerequisite checks (Node, node_modules, port) + `ng serve --open`
4. **start-mobile.bat** — Prerequisite checks (ADB device, port) + `adb reverse` + Metro + `run-android`
5. **GitHub Actions CI workflows:**
   - `ci-api.yml` — build + unit tests + integration tests on `apps/api/**` changes
   - `ci-web.yml` — npm ci + test + build on `apps/web/**` changes
   - `ci-mobile.yml` — npm ci + unit tests + debug APK on `apps/mobile/**` changes
6. **GitHub Actions CD workflow:** `cd-deploy.yml` — manual dispatch
   - Publish API (`dotnet publish --self-contained -r win-x64`)
   - Build Web (`ng build --production`)
   - Copy artifacts to Windows Server (WinRM)
   - Recycle IIS app pool

### WOW (Build Next — High Impact)

- Shared `check-prereqs.bat` module for DRY prerequisite validation
- API health endpoint check in `start-mobile.bat` before starting Metro
- `stop-all.bat` companion script to cleanly kill all processes + docker compose down
- Android release build pipeline with keystore signing in CI

### HOW (Future — Docker Phase)

- Dockerfiles for API and Web
- Full docker-compose.yml with all services including API + Web
- Container registry push + deploy
- Docker-based CD pipeline
- iOS build pipeline (macOS runner)

## Action Plan

### Phase 1: Create Local Dev Scripts

1. Write `scripts/start-docker.bat`
2. Write `scripts/start-api.bat`
3. Write `scripts/start-web.bat`
4. Write `scripts/start-mobile.bat`
5. Test each script individually
6. Test full workflow: Docker → API → Web/Mobile

### Phase 2: Set Up CI Pipelines

1. Create `.github/workflows/ci-api.yml`
2. Create `.github/workflows/ci-web.yml`
3. Create `.github/workflows/ci-mobile.yml`
4. Verify all pass on PR

### Phase 3: Set Up CD Pipeline

1. Create `.github/workflows/cd-deploy.yml`
2. Configure WinRM secrets in GitHub repo
3. Test deployment to Windows Server (IIS)
4. Verify API + Web running after deployment

### Phase 4: Android Release Pipeline

1. Set up Android keystore in GitHub secrets
2. Add release build job to ci-mobile.yml or cd-deploy.yml
3. Publish APK as release artifact

### Phase 5: Docker (Future)

1. Create Dockerfiles
2. Update docker-compose.yml
3. Set up container registry
4. Create Docker-based CD pipeline

## Session Summary

**Total Ideas Generated:** 15 key concepts
**Techniques Used:** Mind Mapping, Reverse Brainstorming, How-Now-Wow Matrix
**Outcome:** Clear phased action plan for local dev scripts, CI/CD pipelines, and deployment strategy

### Key Decisions Made

- **Windows-first** — production server runs Windows + IIS + local PostgreSQL/Redis
- **Non-Docker first** — Docker deployment is Phase 5
- **iOS deferred** — requires macOS/Xcode, revisit in future
- **Environment variables** for API configuration at deploy time
- **Modular GitHub Actions** — separate workflow per app with path triggers
- **Manual CD dispatch** — deployment triggered intentionally, not automatic
- **Prerequisite validation** — scripts check and guide, don't auto-fix

# Self-hosted deployment (docker-compose)

This covers running Midi-Kaval outside Render, via `docker-compose.yml` — Postgres,
Redis, the API, and a Caddy container that serves the Angular web app and
reverse-proxies `/api/*` to the API, with automatic free HTTPS (Let's Encrypt).

## Files involved

| File | Purpose |
|---|---|
| `docker-compose.yml` | Orchestrates all 4 services. |
| `apps/api/Dockerfile` | Builds the .NET API image. |
| `apps/web/Dockerfile` | Builds the Angular app, serves it via Caddy. |
| `apps/web/Caddyfile` | Caddy's routing config (static files + API reverse proxy). |
| `.env.example` | Every config value the stack needs — copy to `.env` and fill in. |
| `.github/workflows/docker-publish.yml` | Builds both images and pushes to GHCR on push to `master` (or manually via the Actions tab). |

## Memory tuning — history and how to re-tune it

Every service in `docker-compose.yml` has a `mem_limit` and, for Postgres/Redis, a
command-line memory setting. These aren't arbitrary — they were originally tuned tight
for a specific constraint, then relaxed once that constraint no longer applied. If you
move this to a *different*-sized machine in the future, re-derive the numbers using the
same reasoning below rather than guessing.

### Version 1 — tuned for a 1GB RAM box (Oracle Cloud Always Free `VM.Standard.E2.1.Micro`)

| Service | mem_limit | Key setting | Why |
|---|---|---|---|
| postgres | 150m | `shared_buffers=32MB`, `max_connections=30` | Minimum viable cache; this app never needs more than a handful of connections. |
| redis | 48m | `maxmemory 32mb` | Only holds OTP challenges / rate-limit counters — small, ephemeral data. |
| api | 400m | `DOTNET_gcServer=0` (workstation GC) | Server GC pre-allocates a heap *per core* and assumes RAM to spare — wrong tradeoff when RAM is the scarce resource. |
| web (Caddy) | 64m | — | Caddy itself is lightweight; this is just a safety cap. |

Total ceiling: ~660MB, leaving ~300-350MB for the OS/Docker daemon on a 1GB host — tight
but workable for light usage only (see the performance tradeoffs discussed when this was
set up: single OCPU contention, report-generation memory spikes, Postgres cache misses
under concurrent load). Also required a 4GB swapfile as a safety net at this size.

### Version 2 (current) — relaxed for a 12GB RAM box (Windows VM)

| Service | mem_limit | Key setting | Why |
|---|---|---|---|
| postgres | 512m | `shared_buffers=128MB`, `max_connections=100` | More realistic default-sized cache now that RAM isn't scarce. |
| redis | 256m | `maxmemory 200mb` | More headroom; still capped so nothing runs unbounded. |
| api | 1536m | *(GC restriction removed)* | Let .NET pick whichever GC mode suits the environment — no longer artificially constrained. |
| web (Caddy) | 128m | — | Still just a safety cap; Caddy doesn't need this much. |

Total ceiling: ~2.4GB, leaving ~9.5GB+ free for Windows/Docker Desktop/WSL2 overhead.
Swap is no longer necessary at this size.

### If you resize again

Rough rule of thumb used above: budget Postgres and the API as the two "real" consumers
(they scale with usage), give Redis a modest fixed cap (this app's Redis usage is
inherently small and bounded), give Caddy a small fixed cap, and always leave at least
25-30% of total RAM unallocated for the OS/Docker itself. Keep `mem_limit`s in place even
on a large box — they're cheap insurance against one runaway container starving the
others, not just a small-box workaround.

## Deploying

1. Copy `.env.example` to `.env` and fill in the blanks (domain, seed admin/vendor
   credentials, B2 and Brevo credentials).
2. Either build locally (`docker compose build`) or pull pre-built images from GHCR
   (`docker compose pull` — requires `docker login ghcr.io` first with a GitHub Personal
   Access Token that has the `read:packages` scope).
3. `docker compose up -d`
4. `docker compose logs -f api` — watch for migrations and account seeding to complete.
5. Visit `https://<your domain>` — Caddy will have automatically obtained a Let's Encrypt
   certificate, provided DNS already points at this machine and ports 80/443 are reachable
   (check both the cloud provider's network firewall *and* the guest OS's own firewall —
   these are two independent layers and both must allow inbound 80/443).

Account seeding (`Seed__Admin__*` / `Seed__Vendor__*` in `.env`) only ever runs once per
email — it's a no-op on every subsequent restart once that account exists, even if you
change the password value afterward. To force a re-seed with a new password, delete the
user's row from the `users` table (via `docker compose exec postgres psql -U kaval`) and
restart the `api` container.

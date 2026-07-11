FROM node:20-alpine AS build
WORKDIR /repo

COPY package.json package-lock.json ./
COPY apps/web/package.json apps/web/package.json
COPY packages/shared-types/package.json packages/shared-types/package.json
COPY packages/api-client/package.json packages/api-client/package.json
RUN npm ci

COPY packages/shared-types packages/shared-types
COPY packages/api-client packages/api-client
COPY apps/web apps/web

# packages/*'s dist/ output is git-ignored (only checked-in TS source), so they must be
# compiled before the web app's own build runs — mirrors render.yaml's buildCommand.
RUN npm run build --workspace=packages/shared-types
RUN npm run build --workspace=packages/api-client
RUN npm run build --workspace=apps/web -- --configuration=production

FROM caddy:2-alpine AS runtime
COPY --from=build /repo/apps/web/dist/web/browser /srv/web
COPY apps/web/Caddyfile /etc/caddy/Caddyfile

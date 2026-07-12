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
# "selfhosted" (not "production"): Caddy reverse-proxies /api/* to the api container
# on the SAME origin here, unlike Render where web and API are separate origins and
# need an absolute cross-origin URL. Using "production" here would bake in Render's
# API URL and cause CORS failures against a completely different backend.
RUN npm run build --workspace=apps/web -- --configuration=selfhosted

FROM caddy:2-alpine AS runtime
COPY --from=build /repo/apps/web/dist/web/browser /srv/web
COPY apps/web/Caddyfile /etc/caddy/Caddyfile

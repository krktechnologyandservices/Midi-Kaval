export const environment = {
  production: true,
  // Empty string, not an absolute URL: in a docker-compose/self-hosted deployment,
  // Caddy reverse-proxies /api/* to the api container on the SAME origin as the web
  // app (see apps/web/Caddyfile), unlike Render where web and API are separate
  // origins and need an absolute cross-origin URL + CORS. A relative path here
  // resolves correctly against whatever domain Caddy is actually serving from.
  apiBaseUrl: '',
};

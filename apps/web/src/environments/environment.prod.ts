export const environment = {
  production: true,
  // Must match the API service's actual Render URL. Render derives this from the
  // service name defined in render.yaml (currently "midi-kaval-api") — if that name
  // changes, update this and redeploy the web app.
  apiBaseUrl: 'https://midi-kaval-api.onrender.com',
};

# @midi-kaval/api-client

TypeScript types generated from the Midi Kaval OpenAPI specification.

**Do not hand-edit** files under `src/generated/`.

## Regenerate

1. Start the API (from repo root):

   ```bash
   dotnet run --project apps/api
   ```

2. Generate types (from repo root):

   ```bash
   npm run generate:api-client
   ```

   Override the OpenAPI URL if needed:

   ```bash
   set API_OPENAPI_URL=http://localhost:5049/swagger/v1/swagger.json
   npm run generate -w @midi-kaval/api-client
   ```

3. Verify compile:

   ```bash
   npm run build -w @midi-kaval/api-client
   ```

OpenAPI document: [http://localhost:5049/swagger/v1/swagger.json](http://localhost:5049/swagger/v1/swagger.json)

Swagger UI: [http://localhost:5049/swagger](http://localhost:5049/swagger)

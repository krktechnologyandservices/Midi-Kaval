import { execSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const packageRoot = path.join(scriptDir, '..');
const monorepoRoot = path.join(packageRoot, '..', '..');
const out = path.join(packageRoot, 'src', 'generated', 'api.ts');

function resolveOpenApiFile(envPath) {
  if (path.isAbsolute(envPath)) {
    return envPath;
  }

  const candidates = [
    path.resolve(process.cwd(), envPath),
    path.resolve(monorepoRoot, envPath),
    path.resolve(packageRoot, envPath),
  ];

  return candidates.find(candidate => existsSync(candidate))
    ?? path.resolve(process.cwd(), envPath);
}

const envFile = process.env.API_OPENAPI_FILE;
const fileSource = envFile ? resolveOpenApiFile(envFile) : null;
const url =
  process.env.API_OPENAPI_URL ?? 'http://localhost:5049/swagger/v1/swagger.json';
const source = fileSource && existsSync(fileSource) ? fileSource : url;

execSync(`npx openapi-typescript "${source}" -o "${out}"`, {
  stdio: 'inherit',
  shell: true,
});

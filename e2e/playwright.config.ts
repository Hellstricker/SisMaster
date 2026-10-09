import { defineConfig } from '@playwright/test';

// Roda contra o ambiente isolado que o scripts/verificar.ps1 sobe (nginx em 3300 -> API -> SQL Server vazio).
// Para outro endereco, defina E2E_BASE_URL. NUNCA aponte para o ambiente de uso diario: o teste cria dados.
export default defineConfig({
  testDir: './tests',
  timeout: 120_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3300',
    locale: 'pt-BR',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});

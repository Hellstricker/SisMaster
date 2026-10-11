import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * V0 — Fundação do site público: a raiz (/) agora é o site (Next.js) atrás do nginx, mostra a associação configurada e as
 * inscrições abertas lidas da API pública (sem login), e a gestão e a área do árbitro continuam nos mesmos endereços.
 * A associação do site no ambiente de teste é a de sigla SITEE2E (scripts/lib.ps1).
 */

const sufixo = Date.now().toString(36);
const EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@teste.local';
const SENHA_INICIAL = process.env.E2E_ADMIN_SENHA_INICIAL ?? 'Teste#Admin2026';
const SENHA_NOVA = 'Admin#E2E2026x';

async function tokenDoAdministrador(request: APIRequestContext): Promise<string> {
  for (const senha of [SENHA_NOVA, SENHA_INICIAL]) {
    const r = await request.post('/api/identidade/autenticar', { data: { email: EMAIL, password: senha } });
    if (!r.ok()) continue;
    const corpo = await r.json();
    if (!corpo.userToken.trocarSenha) return corpo.accessToken as string;
    const troca = await request.post('/api/identidade/trocar-senha', {
      data: { senhaAtual: senha, novaSenha: SENHA_NOVA }, headers: { Authorization: `Bearer ${corpo.accessToken}` },
    });
    expect(troca.ok(), `troca de senha: ${await troca.text()}`).toBe(true);
    return (await troca.json()).accessToken as string;
  }
  throw new Error('Não consegui entrar como administrador de teste.');
}

async function chamar(request: APIRequestContext, token: string, metodo: string, caminho: string, corpo?: unknown) {
  const r = await request.fetch(caminho, { method: metodo, data: corpo, headers: { Authorization: `Bearer ${token}` } });
  const texto = await r.text();
  expect(r.status(), `${metodo} ${caminho} -> ${r.status()}: ${texto}`).toBeLessThan(300);
  return texto ? JSON.parse(texto) : null;
}
const dados = (j: any) => (j && j.data !== undefined && Object.keys(j).length === 1 ? j.data : j);
const dataBr = (d: Date) => `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;

test('V0: o site público abre na raiz com as inscrições abertas e sem dado pessoal', async ({ page, request }) => {
  const hoje = new Date();
  const inicio = new Date(hoje.getTime() - 86_400_000);
  const fim = new Date(hoje.getTime() + 20 * 86_400_000);
  const token = await tokenDoAdministrador(request);

  await test.step('monta a associação do site, uma categoria e uma temporada com inscrições abertas pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: `Master 40+ ${sufixo}`, idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: `Baiano Master ${sufixo}` });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano: hoje.getFullYear(), dataInicioInscricoes: inicio.toISOString(), dataFimInscricoes: fim.toISOString(),
    });
    const temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
  });

  await test.step('a raiz é o site e mostra a associação configurada', async () => {
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Associação do Site E2E');
    await expect(page.locator('.logo')).toContainText('SITEE2E');
    await expect(page).toHaveTitle(/SITEE2E/);
  });

  await test.step('as inscrições abertas aparecem com campeonato, janela, categoria e prazo', async () => {
    const cartao = page.locator('.card', { hasText: `Baiano Master ${sufixo}` });
    await expect(cartao).toBeVisible();
    await expect(cartao).toContainText(`Master 40+ ${sufixo}`);
    await expect(cartao).toContainText(dataBr(inicio));
    await expect(cartao).toContainText(dataBr(fim));
    await expect(cartao).toContainText(/Prazo: (19|20) dias/);
    await expect(cartao.getByText('Inscrição online em breve')).toBeVisible();
    await page.screenshot({ path: 'test-results/v0-inicio.png', fullPage: true });
  });

  await test.step('a gestão e a área do árbitro continuam nos mesmos endereços', async () => {
    await page.getByRole('link', { name: 'Gestão' }).click();
    await expect(page).toHaveURL(/\/admin\/|\/admin\/entrar/);
    await page.goto('/arbitros/');
    await expect(page).toHaveURL(/entrar/);
  });

  await test.step('a API pública responde sem login e rota inexistente do site cai na página 404 do site', async () => {
    const r = await request.get('/api/publico/associacoes/SITEE2E');
    expect(r.status()).toBe(200);
    expect((await r.json()).sigla).toBe('SITEE2E');
    const nf = await page.goto('/nao-existe');
    expect(nf?.status()).toBe(404);
    await expect(page.getByRole('heading', { name: 'Página não encontrada' })).toBeVisible();
  });
});

test('V0: o início cabe na tela do celular, sem rolagem horizontal', async ({ page }) => {
  await page.setViewportSize({ width: 393, height: 851 });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  const largura = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
  expect(largura.doc).toBeLessThanOrEqual(largura.janela);
});

import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * L1 — Política de privacidade e termos de uso no site (/privacidade e /termos), com link no rodapé e os dados da controladora
 * (CNPJ, endereço, encarregado, e-mail de privacidade e foro) vindos do cadastro da associação (gestão > Página pública).
 * A associação do site no ambiente de teste é a de sigla SITEE2E (scripts/lib.ps1); o teste a reaproveita ou cria.
 */

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

test('L1: política de privacidade e termos de uso no site, com os dados da associação', async ({ page, request }) => {
  const token = await tokenDoAdministrador(request);
  let assocId = '';

  await test.step('garante a associação do site e deixa os dados legais em branco', async () => {
    const existente = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E');
    if (!existente) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    assocId = existente?.id ?? (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'PUT', `/api/associacoes/${assocId}/pagina-publica`, { diretoria: [] });
  });

  await test.step('o rodapé leva às duas páginas', async () => {
    await page.goto('/');
    await page.getByRole('contentinfo').getByRole('link', { name: 'Privacidade' }).click();
    await expect(page).toHaveURL(/\/privacidade$/);
    await page.goto('/');
    await page.getByRole('contentinfo').getByRole('link', { name: 'Termos de uso' }).click();
    await expect(page).toHaveURL(/\/termos$/);
  });

  await test.step('sem dados cadastrados, o texto diz "a informar pela associação" e o foro cai no domicílio do usuário', async () => {
    await page.goto('/privacidade');
    await expect(page.getByRole('heading', { level: 1, name: 'Política de privacidade' })).toBeVisible();
    await expect(page.getByText('a informar pela associação').first()).toBeVisible();
    await page.goto('/termos');
    await expect(page.getByText('foro da comarca do domicílio do usuário')).toBeVisible();
  });

  await test.step('na gestão, a diretoria preenche os dados e o site passa a mostrá-los', async () => {
    await chamar(request, token, 'PUT', `/api/associacoes/${assocId}/pagina-publica`, {
      diretoria: [], cnpj: '12345678000195', endereco: 'Rua das Palmeiras, 100 - Salvador/BA', encarregadoDeDados: 'Maria Silva',
      emailPrivacidade: 'privacidade@exemplo.org', foroCidade: 'Salvador/BA',
    });
    await page.goto('/privacidade');
    await expect(page.getByText('12.345.678/0001-95')).toBeVisible();
    await expect(page.getByText('Rua das Palmeiras, 100 - Salvador/BA')).toBeVisible();
    await expect(page.getByText('Maria Silva')).toBeVisible();
    await expect(page.getByText('privacidade@exemplo.org').first()).toBeVisible();
    await expect(page.getByText('a informar pela associação')).toHaveCount(0);
    await page.screenshot({ path: 'test-results/l1-privacidade.png', fullPage: true });
    await page.goto('/termos');
    await expect(page.getByText('Fica eleito o foro da comarca de')).toBeVisible();
    await expect(page.getByText('Salvador/BA').first()).toBeVisible();
    await page.screenshot({ path: 'test-results/l1-termos.png', fullPage: true });
  });

  await test.step('o texto promete o que o sistema faz: sem CPF/e-mail/telefone em páginas públicas e sem rastreamento', async () => {
    await page.goto('/privacidade');
    await expect(page.getByText('CPF, data de nascimento, e-mail e telefone nunca aparecem em páginas públicas')).toBeVisible();
    await expect(page.getByText('não usa cookies de publicidade nem de rastreamento')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Seus direitos/ })).toBeVisible();
  });

  await test.step('as páginas cabem no celular', async () => {
    await page.setViewportSize({ width: 393, height: 851 });
    for (const rota of ['/privacidade', '/termos']) {
      await page.goto(rota);
      const w = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
      expect(w.doc, rota).toBeLessThanOrEqual(w.janela);
    }
  });
});

import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * V5 — "A associação" e "Contato" configuráveis por associação: a gestão preenche a página pública (missão, quem somos, diretoria e
 * canais de contato) e o site mostra só o que está preenchido, com texto simples (HTML digitado aparece como texto, não executa).
 * A associação do site no ambiente de teste é a de sigla SITEE2E (scripts/lib.ps1); o teste a reaproveita ou cria.
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

test('V5: a gestão preenche a página pública e o site mostra só o que está preenchido', async ({ page, request }) => {
  const token = await tokenDoAdministrador(request);
  let assocId = '';

  await test.step('garante a associação do site (reaproveita ou cria)', async () => {
    const existente = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E');
    if (!existente) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    assocId = existente?.id ?? (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    // começa do zero: sem conteúdo
    await chamar(request, token, 'PUT', `/api/associacoes/${assocId}/pagina-publica`, { diretoria: [] });
  });

  await test.step('sem conteúdo o site avisa que ainda não há nada publicado', async () => {
    await page.goto('/a-associacao');
    await expect(page.getByText('ainda não publicou o conteúdo desta página')).toBeVisible();
    await page.goto('/contato');
    await expect(page.getByText('ainda não publicou os canais de contato')).toBeVisible();
  });

  await test.step('na gestão: Detalhe da associação → Página pública', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page).not.toHaveURL(/entrar/);
    await page.goto(`/admin/associacoes/${assocId}`);
    await page.getByRole('button', { name: 'Página pública' }).click();
    await expect(page.getByRole('heading', { name: 'Página pública' })).toBeVisible();
  });

  await test.step('preenche missão, quem somos, diretoria (com um cargo sem nome) e contatos', async () => {
    await page.getByLabel(/Missão/).fill(`Aqui, o basquete vive ${sufixo}.`);
    await page.getByLabel('Quem somos').fill(`Primeiro parágrafo ${sufixo}.\n\nSegundo parágrafo com <b>tag</b> e <script>window.__xss=1</script>.`);
    await page.getByLabel('Gestão').fill('2026/2027');
    for (let i = 0; i < 3; i++) await page.getByRole('button', { name: '+ Adicionar cargo' }).click();
    await page.locator('#pp-cargo-0').fill('Presidente');
    await page.locator('#pp-nome-0').fill(`Ana Souza ${sufixo}`);
    await page.locator('#pp-cargo-1').fill('Secretário');           // sem nome: não vai para o site
    await page.locator('#pp-cargo-2').fill('Tesoureiro');
    await page.locator('#pp-nome-2').fill('Carla Dias');
    await page.getByRole('button', { name: 'Subir Tesoureiro' }).click();   // Tesoureiro passa a ser o 2º
    await page.getByLabel('WhatsApp').fill('(71) 9 8888-7777');
    await page.getByLabel('Instagram').fill('@ababas_oficial');
    await page.getByLabel('E-mail', { exact: true }).fill('contato@exemplo.org');
    await page.getByLabel('Onde jogamos').fill('Ginásio Municipal, Salvador/BA');
    await page.screenshot({ path: 'test-results/v5-gestao.png', fullPage: true });
    await page.getByRole('button', { name: 'Salvar e publicar' }).click();
    await expect(page.getByText('Página salva. Já está no site.')).toBeVisible();
  });

  await test.step('o site mostra a missão, os parágrafos e a diretoria na ordem, sem o cargo sem nome', async () => {
    await page.goto('/a-associacao');
    await expect(page.getByText(`Aqui, o basquete vive ${sufixo}.`)).toBeVisible();
    await expect(page.getByText(`Primeiro parágrafo ${sufixo}.`)).toBeVisible();
    await expect(page.getByText('gestão 2026/2027')).toBeVisible();
    const cargos = await page.locator('.membro .cargo').allTextContents();
    expect(cargos).toEqual(['Presidente', 'Tesoureiro']);
    await expect(page.getByText(`Ana Souza ${sufixo}`)).toBeVisible();
    await expect(page.getByText('Secretário')).toHaveCount(0);
    await page.screenshot({ path: 'test-results/v5-site-associacao.png', fullPage: true });
  });

  await test.step('HTML digitado aparece como texto e não executa', async () => {
    await expect(page.getByText('<b>tag</b>')).toBeVisible();
    expect(await page.evaluate(() => (window as any).__xss)).toBeUndefined();
  });

  await test.step('o contato mostra os canais preenchidos com os links certos', async () => {
    await page.goto('/contato');
    await expect(page.getByRole('link', { name: /WhatsApp/ })).toHaveAttribute('href', 'https://wa.me/5571988887777');
    await expect(page.getByRole('link', { name: /Instagram/ })).toHaveAttribute('href', 'https://www.instagram.com/ababas_oficial/');
    await expect(page.getByRole('link', { name: /E-mail/ })).toHaveAttribute('href', 'mailto:contato@exemplo.org');
    await expect(page.getByText('Ginásio Municipal, Salvador/BA')).toBeVisible();
    await page.screenshot({ path: 'test-results/v5-site-contato.png', fullPage: true });
  });

  await test.step('canal apagado na gestão some do site', async () => {
    await chamar(request, token, 'PUT', `/api/associacoes/${assocId}/pagina-publica`, { missao: 'x', whatsapp: null, instagram: null, email: 'contato@exemplo.org', diretoria: [] });
    await page.goto('/contato');
    await expect(page.getByRole('link', { name: /WhatsApp/ })).toHaveCount(0);
    await expect(page.getByRole('link', { name: /E-mail/ })).toBeVisible();
  });
});

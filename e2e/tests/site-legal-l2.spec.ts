import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * L2 — textos legais versionados: a gestão publica uma versão nova da política de privacidade (texto próprio, com os dados da
 * associação nos marcadores), o site passa a mostrá-la com a versão, a data e o resumo da mudança, os termos continuam no texto
 * padrão, a versão nova é a que o cadastro oferece, e nada do que se escreve vira código na página.
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

test('L2: a gestão publica uma versão nova dos textos legais e o site passa a mostrá-la', async ({ page, request }) => {
  const token = await tokenDoAdministrador(request);
  let associacaoId = '';

  await test.step('a associação do site existe', async () => {
    const lista = dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[];
    if (!lista.find(a => a.nome === 'Associação do Site E2E')) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    associacaoId = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
  });

  let versao = '';
  await test.step('publica pela interface: exige o resumo e sobe para a próxima versão principal', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/associacoes/${associacaoId}/textos-legais`);
    await expect(page.getByRole('heading', { name: 'Textos legais' })).toBeVisible();

    await page.getByRole('button', { name: 'Publicar nova versão' }).click();
    await expect(page.getByText('Descreva o que mudou nesta versão.')).toBeVisible();

    await page.getByLabel(/O que mudou/).fill(`Texto próprio ${sufixo}`);
    await page.getByLabel('Mudança relevante').check();
    await page.getByLabel('Política de privacidade', { exact: true }).fill(
      `## 1. Quem controla ${sufixo}\nA controladora é a **{{nome}}** (CNPJ {{cnpj}}).\n\n- primeiro item\n- segundo item\n\n<script>window.__injetado = 1</script>`);
    await page.getByRole('button', { name: 'Publicar nova versão' }).click();
    const aviso = await page.getByText(/Versão \d+\.0 publicada\. Já está no site\./).textContent();
    versao = /Versão (\d+\.0)/.exec(aviso ?? '')![1];
    await expect(page.getByTestId('vigente')).toHaveText(versao);
    await expect(page.getByText(`Texto próprio ${sufixo}`).last()).toBeVisible();   // no histórico
    await page.screenshot({ path: 'test-results/l2-gestao.png', fullPage: true });
  });

  await test.step('o site mostra a política nova, com os dados da associação, e os termos continuam no padrão', async () => {
    await page.goto('/privacidade');
    await expect(page.getByRole('heading', { name: 'Política de privacidade' })).toBeVisible();
    await expect(page.getByText(`Versão ${versao} · em vigor desde`)).toBeVisible();
    await expect(page.getByRole('heading', { name: `1. Quem controla ${sufixo}` })).toBeVisible();
    await expect(page.getByText('A controladora é a Associação do Site E2E', { exact: false })).toBeVisible();
    await expect(page.locator('li', { hasText: 'segundo item' })).toBeVisible();
    await expect(page.getByText(`O que mudou na versão ${versao}: Texto próprio ${sufixo}`)).toBeVisible();
    // texto escrito pela associação é só texto: o <script> aparece escrito e não roda
    await expect(page.getByText('<script>window.__injetado = 1</script>')).toBeVisible();
    expect(await page.evaluate(() => (window as any).__injetado)).toBeUndefined();
    await page.screenshot({ path: 'test-results/l2-privacidade.png', fullPage: true });

    await page.goto('/termos');
    await expect(page.getByRole('heading', { name: 'Termos de uso' })).toBeVisible();
    await expect(page.getByRole('heading', { name: '1. Aceitação' })).toBeVisible();   // texto padrão do sistema
    await expect(page.getByText(`Versão ${versao} · em vigor desde`)).toBeVisible();
  });

  await test.step('o cadastro oferece o aceite da versão vigente', async () => {
    await page.goto('/entrar?aba=criar');
    await expect(page.getByText(`(versão ${versao})`)).toBeVisible();
  });

  await test.step('o histórico público lista as versões', async () => {
    const r = await request.get('/api/publico/associacoes/SITEE2E/legal');
    const j = await r.json();
    expect(j.versao).toBe(versao);
    expect(j.historico[0].versao).toBe(versao);
    expect(j.historico[0].relevante).toBe(true);
  });
});

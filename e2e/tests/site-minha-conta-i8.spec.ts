import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * I8 — Minha conta: dados do cadastro (CPF mascarado, só o telefone muda), troca de senha (a senha antiga deixa de valer) e as
 * autorizações dadas em cada inscrição. Conta criada pela API, cadastro e inscrição feitos pela API do participante.
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

function cpfValido(n: number): string {
  const base = String(100000000 + n).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => { const s = a.reduce((t, x, i) => t + x * (a.length + 1 - i), 0); const r = (s * 10) % 11; return r === 10 ? 0 : r; };
  const d1 = dv(base);
  return [...base, d1, dv([...base, d1])].join('');
}

async function contaConfirmada(request: APIRequestContext, email: string, nome: string) {
  await request.post('/api/identidade/cadastrar', { data: { nome, email, senha: 'basquete2027', aceiteTermos: true, versaoTermos: '1.0' } });
  let corpo = '';
  for (let i = 0; i < 30 && !corpo; i++) {
    const lista = (await (await request.get(`/api/identidade/teste/emails?para=${encodeURIComponent(email)}`)).json()) as { assunto: string; corpo: string }[];
    corpo = lista.find(e => e.assunto.startsWith('Confirme'))?.corpo ?? '';
    if (!corpo) await new Promise(r => setTimeout(r, 400));
  }
  const link = new URL(corpo.split('\n').map(l => l.trim()).find(l => l.includes('/confirmar-email?'))!);
  const r = await request.post('/api/identidade/confirmar-email', { data: { email: link.searchParams.get('email'), token: link.searchParams.get('token') } });
  expect(r.status(), await r.text()).toBe(200);
  const j = await r.json();
  return { accessToken: j.accessToken as string, expiraEm: Date.now() + j.expiresIn * 1000, usuario: { id: j.userToken.id, email: j.userToken.email, nome: j.userToken.nome, perfil: j.userToken.perfil } };
}

async function conectar(page: Page, sessao: unknown) {
  await page.goto('/');
  await page.evaluate(s => localStorage.setItem('sismaster.site.sessao', JSON.stringify(s)), sessao);
}

test('I8: minha conta com dados, troca de senha e autorizações', async ({ page, request }) => {
  const hoje = new Date();
  const token = await tokenDoAdministrador(request);
  const cpf = cpfValido((Date.now() + 99) % 80_000_000);
  const email = `conta.${sufixo}@exemplo.com`;
  const categoria = `Master Conta ${sufixo}`;
  let temporada = '';
  let categoriaId = '';

  await test.step('prepara uma temporada aberta', async () => {
    const lista = dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[];
    if (!lista.find(a => a.nome === 'Associação do Site E2E')) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, { nome: categoria, idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: true, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1 });
    const cat = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[]).find(c => c.nome === categoria);
    const nomeCamp = `Conta ${sufixo}`;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: nomeCamp });
    const campId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === nomeCamp).id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campId}/temporadas`, {
      ano: hoje.getFullYear(), dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 20 * 86_400_000).toISOString(),
    });
    temporada = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporada}/categorias`, { categoriaId: cat.id });
    categoriaId = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporada}`)).categorias as any[])[0].id;
  });

  const sessao = await contaConfirmada(request, email, 'José Santana');

  await test.step('sem cadastro: a aba de dados explica que ele nasce na primeira inscrição', async () => {
    await conectar(page, sessao);
    await page.goto('/minha-conta');
    await expect(page.getByText('Você ainda não tem cadastro.')).toBeVisible();
    await page.getByRole('tab', { name: 'Autorizações' }).click();
    await expect(page.getByText('Você ainda não deu nenhuma autorização')).toBeVisible();
  });

  await test.step('cria o cadastro e a inscrição pela API do participante', async () => {
    const auth = { Authorization: `Bearer ${sessao.accessToken}` };
    const v = await request.post('/api/participante/vinculo', { headers: auth, data: { nome: 'José Santana', cpf, nascimento: '1975-05-10', sexo: 'Masculino', telefone: '71988887777' } });
    expect(v.ok(), await v.text()).toBe(true);
    const e = await request.post('/api/participante/inscricoes', {
      headers: auth,
      data: { temporadaId: temporada, codigoDeEnvio: crypto.randomUUID(), alturaCm: 182, pesoKg: 84, posicao: 'Ala', possuiPlanoSaude: false, aceiteDados: true, aceiteImagem: true, versaoTermos: '1.0', categoriaIds: [categoriaId] },
    });
    expect(e.ok(), await e.text()).toBe(true);
  });

  await test.step('dados: CPF mascarado e só leitura; o telefone muda e fica salvo', async () => {
    await page.goto('/minha-conta');
    await expect(page.getByLabel('Nome', { exact: true })).toHaveValue('José Santana');
    await expect(page.getByLabel('CPF')).toHaveValue(`***.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-**`);
    await expect(page.getByLabel('CPF')).toHaveAttribute('readonly', '');
    await expect(page.getByLabel('E-mail da conta')).toHaveValue(email);
    await expect(page.getByLabel('Telefone / WhatsApp')).toHaveValue('(71) 98888-7777');
    await page.getByLabel('Telefone / WhatsApp').fill('123');
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Informe o telefone com DDD.')).toBeVisible();
    await page.getByLabel('Telefone / WhatsApp').fill('71977776666');
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Telefone atualizado.')).toBeVisible();
    await page.reload();
    await expect(page.getByLabel('Telefone / WhatsApp')).toHaveValue('(71) 97777-6666');
    await expect(page.locator('body')).not.toContainText(cpf);
  });

  await test.step('autorizações: as duas, com data e versão do termo', async () => {
    await page.getByRole('tab', { name: 'Autorizações' }).click();
    const linhas = page.locator('tbody tr');
    await expect(linhas).toHaveCount(2);
    await expect(linhas.nth(0)).toContainText('Tratamento de dados pessoais');
    await expect(linhas.nth(1)).toContainText('Uso de imagem e dados para divulgação');
    await expect(linhas.nth(0)).toContainText(`Conta ${sufixo} · ${hoje.getFullYear()}`);
    await expect(linhas.nth(0)).toContainText('v1.0');
    await page.screenshot({ path: 'test-results/i8-autorizacoes.png', fullPage: true });
  });

  await test.step('senha: valida, troca, e a senha antiga deixa de entrar', async () => {
    await page.getByRole('tab', { name: 'Senha' }).click();
    await page.getByRole('button', { name: 'Alterar senha' }).click();
    await expect(page.getByText('Informe a senha atual.')).toBeVisible();
    await page.getByLabel('Senha atual').fill('errada123');
    await page.getByLabel('Nova senha', { exact: true }).fill('curta');
    await page.getByRole('button', { name: 'Alterar senha' }).click();
    await expect(page.getByText('8 caracteres ou mais')).toBeVisible();
    await page.getByLabel('Nova senha', { exact: true }).fill('basquete2028');
    await page.getByLabel('Repita a nova senha').fill('basquete2029');
    await page.getByRole('button', { name: 'Alterar senha' }).click();
    await expect(page.getByText('não são iguais')).toBeVisible();
    await page.getByLabel('Repita a nova senha').fill('basquete2028');
    await page.getByRole('button', { name: 'Alterar senha' }).click();
    await expect(page.getByText('Senha atual incorreta.')).toBeVisible();
    await page.getByLabel('Senha atual').fill('basquete2027');
    await page.getByRole('button', { name: 'Alterar senha' }).click();
    await expect(page.getByText('Senha alterada.')).toBeVisible();

    const antiga = await request.post('/api/identidade/autenticar', { data: { email, password: 'basquete2027' } });
    expect(antiga.ok()).toBe(false);
    const nova = await request.post('/api/identidade/autenticar', { data: { email, password: 'basquete2028' } });
    expect(nova.ok()).toBe(true);
    // a sessão continua valendo depois da troca
    await page.goto('/minhas-inscricoes');
    await expect(page.getByRole('heading', { name: 'Minhas inscrições' })).toBeVisible();
    await expect(page.locator('.ficha')).toHaveCount(1);
  });

  await test.step('o nome no cabeçalho leva à conta; sair encerra a sessão; sem conta leva a entrar', async () => {
    await page.goto('/');
    await page.locator('.conta-topo').getByRole('link', { name: 'José' }).click();
    await expect(page).toHaveURL(/\/minha-conta$/);
    await page.setViewportSize({ width: 393, height: 851 });
    await page.getByRole('tab', { name: 'Autorizações' }).click();
    const w = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(w.doc).toBeLessThanOrEqual(w.janela);
    await page.getByRole('button', { name: 'Sair da conta' }).click();
    await page.goto('/minha-conta');
    await expect(page.getByRole('link', { name: 'Entrar ou criar conta' })).toBeVisible();
  });
});

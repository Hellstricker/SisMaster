import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * I6 — Minhas inscrições: lista das fichas da conta com a situação de cada categoria, aviso de "ação necessária" quando algum pedido
 * foi aprovado, estado vazio e entrada obrigatória. Contas e inscrições criadas pela API; a aprovação e a recusa, pela gestão.
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

/** Versão dos termos em vigor: a conta nasce com ela aceita (senão o site pediria novo aceite se outro teste já publicou uma versão relevante). */
async function versaoVigente(request: APIRequestContext): Promise<string> {
  return (await (await request.get('/api/publico/associacoes/SITEE2E/legal')).json()).versao as string;
}

async function contaConfirmada(request: APIRequestContext, email: string, nome: string) {
  await request.post('/api/identidade/cadastrar', { data: { nome, email, senha: 'basquete2027', aceiteTermos: true, versaoTermos: await versaoVigente(request) } });
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

test('I6: minhas inscrições com situação por categoria, ação necessária, vazio e entrada', async ({ page, request }) => {
  const hoje = new Date();
  const token = await tokenDoAdministrador(request);
  const nomes = { master: `Master Min ${sufixo}`, hiper: `Hiper Min ${sufixo}` };
  let temporada = '';
  let categoriaIds: string[] = [];

  await test.step('prepara uma temporada aberta com duas categorias', async () => {
    const lista = dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[];
    if (!lista.find(a => a.nome === 'Associação do Site E2E')) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    for (const [nome, idade] of [[nomes.master, 40], [nomes.hiper, 60]] as const)
      await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, { nome, idadeMinima: idade, sexo: 'Masculino', aceitaAbaixoIdadeMinima: true, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1 });
    const cats = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[]).filter(c => c.nome.endsWith(sufixo));
    const nomeCamp = `Min ${sufixo}`;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: nomeCamp });
    const campId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === nomeCamp).id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campId}/temporadas`, {
      ano: hoje.getFullYear(), dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 20 * 86_400_000).toISOString(),
    });
    temporada = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campId}`)).temporadas[0].id;
    for (const c of cats) await chamar(request, token, 'POST', `/api/temporadas/${temporada}/categorias`, { categoriaId: c.id });
    categoriaIds = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporada}`)).categorias as any[]).map(c => c.id);
  });

  const sessao = await contaConfirmada(request, `minhas.${sufixo}@exemplo.com`, 'José Santana');

  await test.step('sem inscrições: estado vazio com o caminho para as inscrições abertas', async () => {
    await conectar(page, sessao);
    await page.goto('/minhas-inscricoes');
    await expect(page.getByText('Você ainda não fez nenhuma inscrição.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Ver inscrições abertas' })).toBeVisible();
    await expect(page.getByText('Ação necessária')).toHaveCount(0);
  });

  await test.step('depois de se inscrever, a ficha aparece em análise, com o protocolo', async () => {
    const auth = { Authorization: `Bearer ${sessao.accessToken}` };
    const v = await request.post('/api/participante/vinculo', { headers: auth, data: { nome: 'José Santana', cpf: cpfValido(Date.now() % 80_000_000), nascimento: '1975-05-10', sexo: 'Masculino', telefone: '71988887777' } });
    expect(v.ok(), await v.text()).toBe(true);
    const e = await request.post('/api/participante/inscricoes', {
      headers: auth,
      data: { temporadaId: temporada, codigoDeEnvio: crypto.randomUUID(), alturaCm: 182, pesoKg: 84, posicao: 'Ala', possuiPlanoSaude: false, aceiteDados: true, aceiteImagem: true, versaoTermos: '1.0', categoriaIds },
    });
    expect(e.ok(), await e.text()).toBe(true);
    await page.goto('/minhas-inscricoes');
    const ficha = page.locator('.ficha', { hasText: `Min ${sufixo}` });
    await expect(ficha).toContainText(`protocolo #${hoje.getFullYear()}-`);
    await expect(ficha.locator('.badge').first()).toHaveText('Em análise');
    await expect(ficha.locator('.cats')).toContainText(nomes.master);
    await expect(page.getByText('Ação necessária')).toHaveCount(0);
  });

  await test.step('antes da aprovação não aparece o cartão de pagamento e o acompanhamento diz que não precisa fazer nada', async () => {
    await page.goto('/minhas-inscricoes');
    await page.locator('.ficha', { hasText: `Min ${sufixo}` }).click();
    await expect(page.getByTestId('situacao')).toHaveText('Em análise');
    await expect(page.getByTestId('pagamento')).toHaveCount(0);
    await expect(page.locator('.o-que-agora').first()).toContainText('não precisa fazer nada');
    await expect(page.locator('.passos li.agora').first()).toContainText('Aprovado pela diretoria');
  });

  await test.step('a gestão aprova uma categoria e recusa a outra: a ficha pede ação e mostra cada situação', async () => {
    const fichas = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporada}/inscricoes`)) as any[];
    const minha = fichas.find(f => f.pessoa.nome === 'José Santana');
    const pedido = (nome: string) => minha.categorias.find((c: any) => c.nome === nome).id;
    await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${pedido(nomes.master)}/aprovar`, { justificativaExcecao: 'Exceção E2E' });
    await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${pedido(nomes.hiper)}/recusar`, { motivo: 'Fora da idade' });
    await page.goto('/minhas-inscricoes');
    await expect(page.getByText('Ação necessária')).toBeVisible();
    const ficha = page.locator('.ficha', { hasText: `Min ${sufixo}` });
    await expect(ficha.locator('.badge').first()).toHaveText('Aguardando pagamento');
    await expect(ficha.locator('.cats span', { hasText: nomes.master })).toContainText('Aprovada');
    await expect(ficha.locator('.cats span', { hasText: nomes.hiper })).toContainText('Recusada');
    await page.screenshot({ path: 'test-results/i6-minhas-inscricoes.png', fullPage: true });
  });

  await test.step('I7: o cartão abre o detalhe, com linha do tempo por categoria, motivo da recusa e pagamento só leitura', async () => {
    await page.goto('/minhas-inscricoes');
    await page.locator('.ficha', { hasText: `Min ${sufixo}` }).click();
    await expect(page).toHaveURL(/\/minhas-inscricoes\/[0-9a-f-]{36}$/);
    await expect(page.getByTestId('situacao')).toHaveText('Aguardando pagamento');
    const master = page.locator('.card', { hasText: nomes.master }).filter({ has: page.locator('.passos') });
    await expect(master).toContainText('Aprovado pela diretoria');
    await expect(master.locator('.passos li.feito')).toHaveCount(2);        // enviado e aprovado
    await expect(master.locator('.passos li.agora')).toContainText('Inscrição confirmada');
    await expect(master.locator('.o-que-agora')).toContainText('falta pagar a taxa');
    const hiper = page.locator('[data-categoria]', { hasText: nomes.hiper });
    await expect(hiper).toContainText('Recusada');
    await expect(hiper).toContainText('Fora da idade');
    await expect(page.getByText(`Sua inscrição em ${nomes.master} foi aprovada.`)).toBeVisible();
    await expect(page.getByRole('link', { name: 'Nova inscrição' })).toBeVisible();   // há pedido recusado
    await expect(page.getByTestId('pagamento')).toContainText('Pagamento online em breve');
    await expect(page.getByText('182 cm / 84 kg')).toBeVisible();
    await expect(page.getByText(/Dados e imagem · termos v1\.0/)).toBeVisible();
    await page.screenshot({ path: 'test-results/i7-detalhe.png', fullPage: true });

    // a ficha de outra conta não abre: mesma resposta de inexistente
    const url = page.url();
    await conectar(page, await contaConfirmada(request, `outra.${sufixo}@exemplo.com`, 'Maria Souza'));
    await page.goto(url);
    await expect(page.getByRole('heading', { name: 'Inscrição não encontrada' })).toBeVisible();
    await expect(page.locator('body')).not.toContainText('Fora da idade');
    await conectar(page, sessao);
  });

  await test.step('o cabeçalho tem o atalho e, no celular, a página cabe na tela', async () => {
    await page.goto('/');
    await page.getByRole('link', { name: 'Minhas inscrições' }).click();
    await expect(page).toHaveURL(/\/minhas-inscricoes$/);
    await page.setViewportSize({ width: 393, height: 851 });
    await page.goto('/minhas-inscricoes');
    await expect(page.getByRole('heading', { name: 'Minhas inscrições' })).toBeVisible();
    const w = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(w.doc).toBeLessThanOrEqual(w.janela);
  });

  await test.step('sem conta a página leva a entrar', async () => {
    await page.evaluate(() => localStorage.removeItem('sismaster.site.sessao'));
    await page.goto('/minhas-inscricoes');
    await expect(page.getByRole('link', { name: 'Entrar ou criar conta' })).toBeVisible();
  });
});

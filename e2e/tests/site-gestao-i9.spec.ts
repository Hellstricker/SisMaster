import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * I9 — a gestão enxerga o que vem do site (protocolo, origem, conta ligada, autorizações), a aprovação e a recusa avisam a pessoa por
 * e-mail, e o Administrador une um cadastro duplicado ao principal pela interface (as fichas e a conta passam para o principal).
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
  const base = String(100000000 + (n * 7919) % 800000000).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => { const s = a.reduce((t, x, i) => t + x * (a.length + 1 - i), 0); const r = (s * 10) % 11; return r === 10 ? 0 : r; };
  const d1 = dv(base);
  return [...base, d1, dv([...base, d1])].join('');
}
const mascara = (c: string) => `${c.slice(0, 3)}.${c.slice(3, 6)}.${c.slice(6, 9)}-${c.slice(9)}`;

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
  return (await r.json()).accessToken as string;
}

test('I9: gestão com protocolo, origem e conta; e-mails de status; união de cadastros', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const semente = Date.now() % 1_000_000;
  const token = await tokenDoAdministrador(request);
  const nomeSite = `Atleta Site ${sufixo}`;
  const nomePrincipal = `Principal Diretoria ${sufixo}`;
  const cpfPrincipal = cpfValido(semente + 1);
  const cpfDuplicado = cpfValido(semente + 2);
  const emailSite = `i9.site.${sufixo}@exemplo.com`;
  let temporadaId = '';

  await test.step('monta a temporada, um cadastro da diretoria e uma inscrição do site com o CPF digitado errado', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação I9 ${sufixo}`, sigla: 'I9', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação I9 ${sufixo}`).id;
    for (const [nome, idade] of [['Master 40+', 40], ['Master 50+', 50]] as const)
      await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, { nome, idadeMinima: idade, sexo: 'Masculino', aceitaAbaixoIdadeMinima: true, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1 });
    const cats = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[];
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato I9' });
    const camp = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${camp}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${camp}`)).temporadas[0].id;
    for (const c of cats) await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId: c.id });
    const tcs = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias as any[];
    const tc = (nome: string) => tcs.find(t => t.nome === nome).id;

    await chamar(request, token, 'POST', '/api/inscricoes', {
      temporadaId, nome: nomePrincipal, cpf: cpfPrincipal, nascimento: '1975-05-10', sexo: 'Masculino', email: `i9.dir.${sufixo}@exemplo.com`,
      consentimentoLgpd: true, temporadaCategoriaIds: [tc('Master 40+')],
    });

    const sessaoToken = await contaConfirmada(request, emailSite, nomeSite);
    const auth = { Authorization: `Bearer ${sessaoToken}` };
    const v = await request.post('/api/participante/vinculo', { headers: auth, data: { nome: nomeSite, cpf: cpfDuplicado, nascimento: '1975-05-10', sexo: 'Masculino', telefone: '71988887777' } });
    expect(v.ok(), await v.text()).toBe(true);
    const e = await request.post('/api/participante/inscricoes', {
      headers: auth,
      data: { temporadaId, codigoDeEnvio: crypto.randomUUID(), alturaCm: 182, pesoKg: 84, posicao: 'Ala', possuiPlanoSaude: false, aceiteDados: true, aceiteImagem: true, versaoTermos: '1.0', categoriaIds: [tc('Master 50+')] },
    });
    expect(e.ok(), await e.text()).toBe(true);
  });

  const linha = (nome: string) => page.getByRole('row').filter({ hasText: nome });

  await test.step('a gestão vê protocolo, origem e conta ligada', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/temporadas/${temporadaId}/inscricoes`);
    await expect(linha(nomeSite)).toContainText(`Site · #${ano}-0001`);
    await expect(linha(nomeSite)).toContainText('conta ligada');
    await expect(linha(nomeSite)).toContainText('autorizações ok');
    await expect(linha(nomePrincipal)).toContainText('Diretoria');
    await expect(linha(nomePrincipal)).not.toContainText('conta ligada');
  });

  await test.step('ficha da diretoria: aviso por e-mail é opcional; ficha do site não pergunta; associado é por temporada', async () => {
    await expect(linha(nomePrincipal)).toContainText('Não associado na temporada');
    await linha(nomePrincipal).getByRole('button', { name: 'Aprovar' }).click();
    await expect(page.getByLabel(/Avisar a pessoa por e-mail/)).toBeVisible();
    await page.getByRole('button', { name: 'Cancelar' }).click();
    await linha(nomeSite).getByRole('button', { name: 'Aprovar' }).click();
    await expect(page.getByLabel(/Avisar a pessoa por e-mail/)).toHaveCount(0);
    await page.getByRole('button', { name: 'Cancelar' }).click();
  });

  await test.step('autorizar a ligação do cadastro da diretoria com uma conta de outro e-mail', async () => {
    await linha(nomePrincipal).getByRole('button', { name: /Ações/ }).click();
    // o menu não pode ser cortado pela tabela: fica inteiro dentro da janela
    const caixa = await page.getByRole('menu').boundingBox();
    const janela = page.viewportSize()!;
    expect(caixa!.y).toBeGreaterThanOrEqual(0);
    expect(caixa!.y + caixa!.height).toBeLessThanOrEqual(janela.height);
    expect(caixa!.x + caixa!.width).toBeLessThanOrEqual(janela.width);
    await page.getByRole('menuitem', { name: /Autorizar ligação com conta do site/ }).click();
    await page.getByLabel('E-mail da conta do site').fill('sem-arroba');
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Informe um e-mail válido')).toBeVisible();
    await page.getByLabel('E-mail da conta do site').fill(`outra.conta.${sufixo}@exemplo.com`);
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Ligação autorizada e registrada na auditoria.')).toBeVisible();
    await expect(linha(nomePrincipal)).toContainText(`ligação autorizada para outra.conta.${sufixo}@exemplo.com`);
  });

  await test.step('aprovar pela interface (o e-mail de aviso é conferido no teste de API)', async () => {
    await linha(nomeSite).getByRole('button', { name: 'Aprovar' }).click();
    await page.getByRole('button', { name: /^Aprovar/ }).last().click();
    await expect(page.getByText('Inscrição aprovada.')).toBeVisible();
  });

  await test.step('unir: exige o CPF e o duplicado some, levando a ficha e a conta para o principal', async () => {
    await linha(nomePrincipal).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: /Unir cadastro duplicado/ }).click();
    await page.getByRole('button', { name: 'Unir cadastros' }).click();
    await expect(page.getByText('Informe o CPF completo do cadastro duplicado.')).toBeVisible();
    await page.getByLabel('CPF do cadastro duplicado').fill(mascara(cpfDuplicado));
    await page.screenshot({ path: 'test-results/i9-unir.png' });
    await page.getByRole('button', { name: 'Unir cadastros' }).click();
    await expect(page.getByText('Cadastros unidos e registrados na auditoria.')).toBeVisible();

    await expect(linha(nomeSite)).toHaveCount(0);
    const principal = page.getByRole('row').filter({ hasText: nomePrincipal });
    await expect(principal).toHaveCount(2);   // as duas fichas agora são do principal
    await expect(principal.filter({ hasText: 'Site · #' })).toContainText('conta ligada');
    const fichas = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    expect(new Set(fichas.map(f => f.pessoa.id)).size).toBe(1);
  });
});

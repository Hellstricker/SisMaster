import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * Fase 2 — inscrição de equipe em torneio aberto, de ponta a ponta: o responsável escolhe "equipe", monta o elenco, cada atleta confirma o
 * convite recebido por e-mail com a própria conta, a diretoria aprova a equipe e a taxa de cada atleta o coloca no elenco.
 * Contas e a temporada pela API; o responsável e o primeiro atleta usam as telas.
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

/** O código do link do convite que a API "enviou" por e-mail (modo Memoria). */
async function codigoDoConvite(request: APIRequestContext, email: string): Promise<string> {
  for (let i = 0; i < 30; i++) {
    const lista = (await (await request.get(`/api/teste/emails?para=${encodeURIComponent(email)}`)).json()) as { assunto: string; corpo: string }[];
    const m = lista.find(e => e.assunto.startsWith('Convite para a equipe'))?.corpo.match(/\/convites\/(\S+)/);
    if (m) return m[1];
    await new Promise(r => setTimeout(r, 400));
  }
  throw new Error(`Não chegou o convite para ${email}`);
}

test('F2: equipe inscrita pelo responsável, confirmada pelos atletas, aprovada pela diretoria e efetivada pela taxa', async ({ page, request }) => {
  const hoje = new Date();
  const token = await tokenDoAdministrador(request);
  const nomeCamp = `Verão ${sufixo}`;
  const nomeCategoria = `Livre 35+ ${sufixo}`;
  let temporada = '';
  const base = Date.now() % 70_000_000;

  await test.step('prepara um torneio aberto com taxa por atleta', async () => {
    const lista = dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[];
    if (!lista.find(a => a.nome === 'Associação do Site E2E')) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, { nome: nomeCategoria, idadeMinima: 35, sexo: 'Masculino', aceitaAbaixoIdadeMinima: true, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1 });
    const cat = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[]).find(c => c.nome === nomeCategoria).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: nomeCamp });
    const campId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === nomeCamp).id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campId}/temporadas`, {
      ano: hoje.getFullYear(), dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 20 * 86_400_000).toISOString(),
    });
    temporada = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporada}/categorias`, { categoriaId: cat });
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporada}/publico`, { publico: 'Aberto' });
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporada}/taxa-inscricao`, { taxa: 150 });
  });

  const emailResp = `resp.${sufixo}@exemplo.com`;
  const sessaoResp = await contaConfirmada(request, emailResp, 'Maria Responsável');
  const atletas = Array.from({ length: 5 }, (_, i) => ({
    nome: `Atleta${i + 1} Verao${sufixo.slice(-3)}`, cpf: cpfValido(base + i * 13), email: `atleta${i + 1}.${sufixo}@exemplo.com`,
  }));

  await test.step('em torneio aberto, a pessoa escolhe como participar antes do formulário', async () => {
    await conectar(page, sessaoResp);
    await page.goto(`/inscricao/${temporada}`);
    await expect(page.getByRole('heading', { name: 'Como você quer participar?' })).toBeVisible();
    await expect(page.getByText('R$ 150,00 por atleta').first()).toBeVisible();
    await expect(page.getByText('não torna ninguém associado')).toBeVisible();
    await page.getByTestId('opcao-equipe').click();
    await expect(page).toHaveURL(new RegExp(`/inscricao/${temporada}/equipe$`));
  });

  await test.step('o responsável monta a equipe e adiciona os atletas pela tela', async () => {
    await page.locator('#e-tel').fill('71 98888-7777');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await page.locator('#e-nome').fill(`Tubarões ${sufixo}`);
    await page.locator('#e-cat').selectOption({ label: nomeCategoria });
    await page.getByRole('button', { name: 'Continuar' }).click();

    // menos de 5 atletas não passa
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.locator('p.aviso[role="alert"]')).toContainText('pelo menos 5 atletas');

    for (const a of atletas) {
      await page.locator('#a-nome').fill(a.nome);
      await page.locator('#a-cpf').fill(a.cpf);
      await page.locator('#a-nasc').fill('15/03/1980');
      await page.locator('#a-email').fill(a.email);
      await page.locator('#a-adicionar').click();
    }
    await expect(page.getByTestId('contagem')).toHaveText('5 atletas');
    // CPF repetido é barrado na tela
    await page.locator('#a-nome').fill('Repetido Elenco');
    await page.locator('#a-cpf').fill(atletas[0].cpf);
    await page.locator('#a-nasc').fill('15/03/1980');
    await page.locator('#a-email').fill('repetido@exemplo.com');
    await page.locator('#a-adicionar').click();
    await expect(page.locator('p.aviso[role="alert"]')).toContainText('já está no elenco');

    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Revise e envie' })).toBeVisible();
    await expect(page.getByText('Nada é cobrado agora')).toBeVisible();
    await page.getByRole('button', { name: 'Enviar equipe' }).click();
    await expect(page.getByTestId('protocolo-equipe')).toContainText(`#E${hoje.getFullYear()}-`);
  });

  let equipeUrl = '';
  await test.step('a equipe aparece em "Minhas inscrições" aguardando as confirmações', async () => {
    await page.getByRole('link', { name: 'Acompanhar minha equipe' }).click();
    await expect(page.getByTestId('nome-equipe')).toHaveText(`Tubarões ${sufixo}`);
    await expect(page.getByTestId('situacao-equipe')).toHaveText('Aguardando confirmações');
    equipeUrl = new URL(page.url()).pathname;
    for (const a of atletas) await expect(page.locator(`tr[data-atleta="${a.nome}"]`)).toContainText('Convite enviado');
    await page.goto('/minhas-inscricoes');
    await expect(page.locator('.ficha', { hasText: `Tubarões ${sufixo}` })).toContainText('0 de 5 atletas confirmaram');
  });

  const codigos: string[] = [];
  for (const a of atletas) codigos.push(await codigoDoConvite(request, a.email));

  await test.step('o link do convite abre sem login, mostrando só o essencial', async () => {
    await page.evaluate(() => localStorage.removeItem('sismaster.site.sessao'));
    await page.goto(`/convites/${codigos[0]}`);
    await expect(page.getByText(`Tubarões ${sufixo}`).first()).toBeVisible();
    await expect(page.getByRole('link', { name: 'Criar conta' })).toBeVisible();
    await expect(page.locator('body')).not.toContainText(atletas[0].cpf);
    await expect(page.locator('body')).not.toContainText(atletas[0].email);
  });

  await test.step('o primeiro atleta confirma pela tela, com a conta do e-mail convidado', async () => {
    const sessao = await contaConfirmada(request, atletas[0].email, atletas[0].nome);
    await conectar(page, sessao);
    await page.goto(`/convites/${codigos[0]}`);
    await page.locator('#c-tel').fill('71 97777-6666');
    await page.getByRole('button', { name: 'Continuar' }).click();
    // sem as duas autorizações não confirma
    await page.locator('#c-confirmar').click();
    await expect(page.locator('p.aviso[role="alert"]')).toContainText('duas autorizações');
    await page.locator('#c-dados').check();
    await page.locator('#c-imagem').check();
    await page.locator('#c-confirmar').click();
    await expect(page.getByTestId('protocolo-convite')).toContainText(`#${hoje.getFullYear()}-`);
  });

  await test.step('os outros quatro confirmam pela API e o responsável vê a equipe em análise', async () => {
    for (let i = 1; i < atletas.length; i++) {
      const sessao = await contaConfirmada(request, atletas[i].email, atletas[i].nome);
      const r = await request.post(`/api/participante/convites/${codigos[i]}/confirmar`, {
        headers: { Authorization: `Bearer ${sessao.accessToken}` },
        data: { telefone: '71977776666', alturaCm: 180, pesoKg: 80, posicao: 'Ala', possuiPlanoSaude: false, aceiteDados: true, aceiteImagem: true, versaoTermos: await versaoVigente(request) },
      });
      expect(r.status(), await r.text()).toBe(200);
    }
    await conectar(page, sessaoResp);
    await page.goto(equipeUrl);
    await expect(page.getByTestId('situacao-equipe')).toHaveText('Em análise');
    await expect(page.getByText('5 confirmados')).toBeVisible();
  });

  await test.step('a diretoria aprova a equipe pela tela: o responsável vê o aviso e as taxas pendentes', async () => {
    await page.evaluate(() => localStorage.removeItem('sismaster.site.sessao'));
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/temporadas/${temporada}`);
    await expect(page.getByText('Equipes inscritas').first()).toBeVisible();
    await page.goto(`/admin/temporadas/${temporada}/equipes-inscritas`);
    const linha = page.getByRole('row').filter({ hasText: `Tubarões ${sufixo}` });
    await expect(linha).toContainText('Em análise');
    await expect(linha).toContainText('5 confirmados');
    await linha.getByRole('button', { name: 'Aprovar' }).click();
    await page.getByRole('button', { name: 'Aprovar equipe' }).click();
    await expect(linha).toContainText('Aprovada');

    // a taxa de dois atletas os efetiva e os coloca no elenco; a equipe não espera os outros
    await linha.getByRole('button', { name: 'Ver atletas e taxas' }).click();
    for (const a of atletas.slice(0, 2)) await page.getByLabel(`Selecionar ${a.nome}`).check();
    await expect(page.getByText('2 selecionados')).toBeVisible();
    await page.locator('#registrar-taxas').click();
    await page.getByRole('button', { name: 'Registrar', exact: true }).click();
    await expect(page.getByText('2 taxas registradas')).toBeVisible();

    const equipes = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporada}/equipes-inscritas`)).equipes as any[];
    const equipe = equipes.find(e => e.nome === `Tubarões ${sufixo}`);
    expect(equipe.situacao).toBe('Aprovada');
    const elenco = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporada}/equipes`));
    const eq = elenco.categorias.flatMap((c: any) => c.equipes).find((e: any) => e.nome === `Tubarões ${sufixo}`);
    expect(eq.atletas).toHaveLength(2);

    // o responsável acompanha: aprovada, 2 efetivados no elenco e as demais taxas a pagar
    await conectar(page, sessaoResp);
    await page.goto(equipeUrl);
    await expect(page.getByTestId('situacao-equipe')).toHaveText('Equipe aprovada');
    await expect(page.locator('tr[data-atleta]', { hasText: 'Paga · no elenco' })).toHaveCount(2);
    await expect(page.locator('tr[data-atleta]', { hasText: 'A pagar' })).toHaveCount(3);
    await expect(page.getByText('2 efetivados')).toBeVisible();
  });
});

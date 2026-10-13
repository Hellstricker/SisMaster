import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * I0 + I4 — conta do público dentro da inscrição: "Quero me inscrever" leva a criar a conta, o e-mail é confirmado pelo link, a
 * pessoa volta direto à inscrição já conectada; entrar/sair, esqueci a senha e o bloqueio da gestão para quem é participante.
 * Os e-mails são lidos da rota de teste da Identidade (Email:Provedor = Memoria, só no ambiente de teste).
 */

const sufixo = Date.now().toString(36);
const EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@teste.local';
const SENHA_INICIAL = process.env.E2E_ADMIN_SENHA_INICIAL ?? 'Teste#Admin2026';
const SENHA_NOVA = 'Admin#E2E2026x';
const EMAIL_PARTICIPANTE = `jose.${sufixo}@exemplo.com`;
const SENHA_PARTICIPANTE = 'basquete2027';

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

/** O último e-mail com esse assunto enviado ao endereço (espera até chegar). */
async function ultimoEmail(request: APIRequestContext, para: string, assuntoComeca: string, quantosJa = 0): Promise<{ assunto: string; corpo: string }> {
  for (let i = 0; i < 30; i++) {
    const r = await request.get(`/api/identidade/teste/emails?para=${encodeURIComponent(para)}`);
    expect(r.status(), 'a rota de e-mails de teste só existe no ambiente de teste').toBe(200);
    const lista = ((await r.json()) as { assunto: string; corpo: string }[]).filter(e => e.assunto.startsWith(assuntoComeca));
    if (lista.length > quantosJa) return lista[lista.length - 1];
    await new Promise(res => setTimeout(res, 500));
  }
  throw new Error(`Nenhum e-mail "${assuntoComeca}" chegou para ${para}`);
}
const linkDoEmail = (corpo: string, trecho: string) => corpo.split('\n').map(l => l.trim()).find(l => l.includes(trecho))!.replace(/^https?:\/\/[^/]+/, '');

async function preencherCadastro(page: Page, email: string) {
  await page.getByLabel('Nome completo').fill('José Santana');
  await page.getByLabel('E-mail').fill(email);
  await page.getByLabel('Senha', { exact: true }).fill(SENHA_PARTICIPANTE);
}

test('I0/I4: conta do público dentro do fluxo da inscrição', async ({ page, request }) => {
  const hoje = new Date();
  const token = await tokenDoAdministrador(request);
  let temporadaId = '';

  await test.step('prepara uma temporada com inscrições abertas na associação do site', async () => {
    const existente = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E');
    if (!existente) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = existente?.id ?? (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: `Master Conta ${sufixo}`, idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[]).find(c => c.nome === `Master Conta ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: `Conta ${sufixo}` });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === `Conta ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano: hoje.getFullYear(), dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 20 * 86_400_000).toISOString(),
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
  });

  await test.step('"Quero me inscrever" sem conta leva a entrar ou criar conta, com o campeonato à vista', async () => {
    await page.goto('/');
    await page.locator('.card', { hasText: `Conta ${sufixo}` }).getByRole('link', { name: 'Quero me inscrever' }).click();
    await expect(page).toHaveURL(new RegExp(`/inscricao/${temporadaId}$`));
    await page.getByRole('link', { name: 'Entrar ou criar conta' }).click();
    await expect(page).toHaveURL(/\/entrar\?voltar=/);
    await expect(page.getByText(`Você escolheu Conta ${sufixo}`)).toBeVisible();
  });

  await test.step('criar conta exige o aceite e valida os campos', async () => {
    const criar = page.getByRole('button', { name: 'Criar conta e continuar' });
    await expect(criar).toBeDisabled();
    await page.getByLabel(/Li e aceito/).check();
    await expect(criar).toBeEnabled();
    await page.getByLabel('Nome completo').fill('José Santana');
    await page.getByLabel('E-mail').fill('sem-arroba');
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_PARTICIPANTE);
    await criar.click();
    await expect(page.getByText('E-mail inválido.')).toBeVisible();
    await page.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await page.getByLabel('Senha', { exact: true }).fill('curta1');
    await criar.click();
    await expect(page.getByText('A senha precisa de 8 caracteres, com letra e número.')).toBeVisible();
  });

  await test.step('cria a conta: aparece "Confirme seu e-mail" e o e-mail sai com o link', async () => {
    await preencherCadastro(page, EMAIL_PARTICIPANTE);
    await page.getByRole('button', { name: 'Criar conta e continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Confirme seu e-mail' })).toBeVisible();
    await expect(page.getByText(EMAIL_PARTICIPANTE)).toBeVisible();
    await page.screenshot({ path: 'test-results/i0-confirme-seu-email.png' });
    const e = await ultimoEmail(request, EMAIL_PARTICIPANTE, 'Confirme');
    expect(e.corpo).toContain('/confirmar-email?');
    expect(e.corpo).toContain(`voltar=${encodeURIComponent(`/inscricao/${temporadaId}`)}`);
  });

  await test.step('e-mail repetido mostra a mesma tela e não cria outra conta', async () => {
    const outra = await page.context().newPage();
    await outra.goto(`/entrar?voltar=${encodeURIComponent(`/inscricao/${temporadaId}`)}`);
    await outra.getByLabel('Nome completo').fill('Outra Pessoa');
    await outra.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await outra.getByLabel('Senha', { exact: true }).fill('OutraSenha99');
    await outra.getByLabel(/Li e aceito/).check();
    await outra.getByRole('button', { name: 'Criar conta e continuar' }).click();
    await expect(outra.getByRole('heading', { name: 'Confirme seu e-mail' })).toBeVisible();
    await outra.close();
    // o e-mail novo é de aviso, não de confirmação, só depois da confirmação (a conta ainda não está confirmada: não há aviso de "já tem conta")
  });

  await test.step('antes de confirmar, entrar avisa e oferece reenviar o e-mail', async () => {
    const aba = await page.context().newPage();
    await aba.goto('/entrar?aba=entrar');
    await aba.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await aba.getByLabel('Senha', { exact: true }).fill(SENHA_PARTICIPANTE);
    await aba.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(aba.getByText('Você ainda não confirmou o e-mail desta conta.')).toBeVisible();
    await expect(aba.getByRole('button', { name: 'Reenviar e-mail de confirmação' })).toBeVisible();
    await aba.close();
  });

  await test.step('abre o link do e-mail: confirma, já conecta e volta direto à inscrição', async () => {
    const e = await ultimoEmail(request, EMAIL_PARTICIPANTE, 'Confirme');
    await page.goto(linkDoEmail(e.corpo, '/confirmar-email?'));
    await expect(page.getByRole('heading', { name: 'E-mail confirmado' })).toBeVisible();
    await page.screenshot({ path: 'test-results/i0-email-confirmado.png' });
    await page.getByRole('link', { name: 'Continuar a inscrição' }).click();
    await expect(page).toHaveURL(new RegExp(`/inscricao/${temporadaId}$`));
    await expect(page.getByRole('heading', { name: 'Seus dados' })).toBeVisible();   // já conectada, direto no 1º passo da inscrição
    await expect(page.locator('.conta-topo')).toContainText('José');
  });

  await test.step('o link é de uso único', async () => {
    const e = await ultimoEmail(request, EMAIL_PARTICIPANTE, 'Confirme');
    await page.goto(linkDoEmail(e.corpo, '/confirmar-email?'));
    await expect(page.getByRole('heading', { name: 'Link inválido ou vencido' })).toBeVisible();
  });

  await test.step('sair e entrar de novo com a senha', async () => {
    await page.goto('/');
    await page.getByRole('button', { name: 'Sair' }).click();
    await expect(page.getByRole('banner').getByRole('link', { name: 'Entrar' })).toBeVisible();
    await page.goto('/entrar?aba=entrar');
    await page.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await page.getByLabel('Senha', { exact: true }).fill('senha-errada1');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(page.getByText('E-mail ou senha incorretos.')).toBeVisible();
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_PARTICIPANTE);
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('.conta-topo')).toContainText('José');
  });

  await test.step('esqueci a senha: o link leva ao site e a nova senha entra', async () => {
    await page.getByRole('button', { name: 'Sair' }).click();
    await page.goto('/esqueci-senha');
    await page.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await page.getByRole('button', { name: 'Enviar link' }).click();
    await expect(page.getByText('Se o e-mail estiver cadastrado, enviamos um link')).toBeVisible();
    const e = await ultimoEmail(request, EMAIL_PARTICIPANTE, 'Recuperação');
    expect(e.corpo).toContain('/redefinir-senha?');
    expect(e.corpo).not.toContain('/admin/redefinir-senha');
    await page.goto(linkDoEmail(e.corpo, '/redefinir-senha?'));
    await page.getByLabel('Nova senha', { exact: true }).fill('novaSenha2027');
    await page.getByLabel('Repita a nova senha').fill('outraDiferente1');
    await page.getByRole('button', { name: 'Salvar nova senha' }).click();
    await expect(page.getByText('As duas senhas não são iguais.')).toBeVisible();
    await page.getByLabel('Repita a nova senha').fill('novaSenha2027');
    await page.getByRole('button', { name: 'Salvar nova senha' }).click();
    await expect(page.getByRole('heading', { name: 'Senha alterada' })).toBeVisible();
    await page.getByRole('main').getByRole('link', { name: 'Entrar' }).click();
    await page.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await page.getByLabel('Senha', { exact: true }).fill('novaSenha2027');
    await page.getByRole('button', { name: 'Entrar', exact: true }).click();
    await expect(page.locator('.conta-topo')).toContainText('José');
  });

  await test.step('a conta do público não alcança a gestão: a tela recusa e a API devolve 403', async () => {
    const login = await request.post('/api/identidade/autenticar', { data: { email: EMAIL_PARTICIPANTE, password: 'novaSenha2027' } });
    expect(login.status()).toBe(200);
    const tokenDoParticipante = (await login.json()).accessToken as string;
    for (const rota of ['/api/associacoes', '/api/arbitros/jogos', '/api/identidade/usuarios']) {
      const r = await request.get(rota, { headers: { Authorization: `Bearer ${tokenDoParticipante}` } });
      expect(r.status(), rota).toBe(403);
    }
    const gestao = await page.context().newPage();
    await gestao.goto('/admin/entrar');
    await gestao.getByLabel('E-mail').fill(EMAIL_PARTICIPANTE);
    await gestao.getByLabel('Senha', { exact: true }).fill('novaSenha2027');
    await gestao.getByRole('button', { name: 'Entrar' }).click();
    await expect(gestao.getByText('Esta conta é do site público')).toBeVisible();
    await gestao.close();
  });

  await test.step('no celular as telas de conta cabem na tela', async () => {
    await page.getByRole('button', { name: 'Sair' }).click();
    await page.setViewportSize({ width: 393, height: 851 });
    for (const rota of ['/entrar', '/entrar?aba=entrar', '/esqueci-senha']) {
      await page.goto(rota);
      await expect(page.getByRole('main')).toBeVisible();
      const w = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
      expect(w.doc, rota).toBeLessThanOrEqual(w.janela);
    }
  });
});

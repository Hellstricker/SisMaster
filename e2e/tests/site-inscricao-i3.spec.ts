import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * I2/I3/I5 — o assistente de inscrição pela conta do site: dados (com o vínculo pelo CPF), ficha, categorias (fora do esperado e já
 * pedidas), autorizações, revisão e o protocolo; quem já se inscreveu antes só confere os dados e tem a ficha preenchida; CPF de outro
 * cadastro e menor de 18 anos são bloqueados. Contas criadas pela API (e-mail lido da rota de teste da Identidade) e conectadas direto.
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
const mascara = (c: string) => `${c.slice(0, 3)}.${c.slice(3, 6)}.${c.slice(6, 9)}-${c.slice(9)}`;

/** Cria a conta do público pela API (cadastro + link do e-mail) e devolve a sessão para o navegador. */
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
  return { accessToken: j.accessToken, expiraEm: Date.now() + j.expiresIn * 1000, usuario: { id: j.userToken.id, email: j.userToken.email, nome: j.userToken.nome, perfil: j.userToken.perfil } };
}

async function conectar(page: Page, sessao: unknown) {
  await page.goto('/');
  await page.evaluate(s => localStorage.setItem('sismaster.site.sessao', JSON.stringify(s)), sessao);
}

test('I2/I3/I5: assistente de inscrição pela conta, com vínculo pelo CPF e protocolo', async ({ page, request }) => {
  const hoje = new Date();
  const token = await tokenDoAdministrador(request);
  const temporadas: string[] = [];
  const categorias: Record<string, string> = {};
  const cpf = cpfValido(Date.now() % 80_000_000);

  await test.step('prepara duas temporadas abertas (Master 40+ e Hipermaster 60+) na associação do site', async () => {
    const existente = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E');
    if (!existente) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = existente?.id ?? (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    for (const [nome, idade] of [[`Master Insc ${sufixo}`, 40], [`Hiper Insc ${sufixo}`, 60]] as const)
      await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, { nome, idadeMinima: idade, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1 });
    const cats = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[];
    for (const nomeCamp of [`Insc A ${sufixo}`, `Insc B ${sufixo}`]) {
      await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: nomeCamp });
      const campId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === nomeCamp).id;
      await chamar(request, token, 'POST', `/api/campeonatos/${campId}/temporadas`, {
        ano: hoje.getFullYear(), dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 20 * 86_400_000).toISOString(),
      });
      const tempId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campId}`)).temporadas[0].id;
      for (const c of cats.filter(x => x.nome.endsWith(sufixo))) await chamar(request, token, 'POST', `/api/temporadas/${tempId}/categorias`, { categoriaId: c.id });
      temporadas.push(tempId);
    }
    categorias.master = `Master Insc ${sufixo}`;
    categorias.hiper = `Hiper Insc ${sufixo}`;
  });

  const email = `atleta.${sufixo}@exemplo.com`;
  await test.step('conta nova (cadastro e confirmação do e-mail pela API) conectada no navegador', async () => {
    await conectar(page, await contaConfirmada(request, email, 'José Santana'));
  });

  await test.step('passo 1: valida CPF e data, depois cria o cadastro ligado à conta', async () => {
    await page.goto(`/inscricao/${temporadas[0]}`);
    await expect(page.getByRole('heading', { name: 'Seus dados' })).toBeVisible();
    await page.getByLabel('CPF').fill('123.456.789-00');
    await page.getByLabel('Data de nascimento').fill('10/05/1975');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('CPF inválido')).toBeVisible();
    await page.getByLabel('CPF').fill(mascara(cpf));
    await page.getByLabel('Data de nascimento').fill('31/02/1975');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('data de nascimento válida')).toBeVisible();
    await page.getByLabel('Data de nascimento').fill('10/05/1975');
    await page.getByLabel('Telefone / WhatsApp').fill('71988887777');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Ficha esportiva' })).toBeVisible();
  });

  await test.step('passo 2: valida a ficha', async () => {
    await page.getByLabel('Altura (cm)').fill('90');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('altura deve estar entre 100 e 260')).toBeVisible();
    await page.getByLabel('Altura (cm)').fill('182');
    await page.getByLabel('Peso (kg)').fill('84');
    await page.getByLabel('Posição').selectOption('Ala');
    await page.getByLabel('Possui plano de saúde?').selectOption('sim');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Informe o nome do plano de saúde')).toBeVisible();
    await page.getByLabel('Possui plano de saúde?').selectOption('nao');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Categorias' })).toBeVisible();
  });

  await test.step('passo 3: avisa "fora do esperado" e exige ao menos uma categoria', async () => {
    await expect(page.getByText(/Idade considerada na temporada:/)).toContainText('51 anos');
    await expect(page.locator('.cat', { hasText: categorias.hiper })).toContainText('fora do esperado');
    await expect(page.locator('.cat', { hasText: categorias.master })).not.toContainText('fora do esperado');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Escolha ao menos uma categoria.')).toBeVisible();
    await page.locator('.cat', { hasText: categorias.master }).getByRole('checkbox').check();
    await page.locator('.cat', { hasText: categorias.hiper }).getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Continuar' }).click();
  });

  await test.step('passo 4: as duas autorizações são obrigatórias', async () => {
    await expect(page.getByRole('heading', { name: 'Autorizações' })).toBeVisible();
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Marque as duas autorizações')).toBeVisible();
    await page.getByRole('checkbox').first().check();
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Marque as duas autorizações')).toBeVisible();
    await page.getByRole('checkbox').nth(1).check();
    await page.getByRole('button', { name: 'Continuar' }).click();
  });

  await test.step('passo 5: revisão com o CPF mascarado, aviso da categoria fora do esperado e envio com protocolo', async () => {
    await expect(page.getByRole('heading', { name: 'Revise e envie' })).toBeVisible();
    await expect(page.getByText(`***.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-**`)).toBeVisible();
    await expect(page.getByText(cpf, { exact: false })).toHaveCount(0);
    await expect(page.getByText(`${categorias.hiper}: 51 anos (mínimo 60)`)).toBeVisible();
    await page.screenshot({ path: 'test-results/i3-revisao.png', fullPage: true });
    await page.getByRole('button', { name: 'Enviar inscrição' }).click();
    await expect(page.getByRole('heading', { name: 'Inscrição enviada!' })).toBeVisible();
    await expect(page.getByTestId('protocolo')).toHaveText(new RegExp(`Protocolo #${hoje.getFullYear()}-0001`));
    await page.screenshot({ path: 'test-results/i3-enviada.png' });
  });

  await test.step('na gestão a ficha aparece com as duas categorias pendentes', async () => {
    const fichas = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadas[0]}/inscricoes`)) as any[];
    const minha = fichas.find(f => f.pessoa.nome === 'José Santana');
    expect(minha.categorias).toHaveLength(2);
  });

  await test.step('abrir de novo: as categorias já pedidas ficam bloqueadas com o protocolo', async () => {
    await page.goto(`/inscricao/${temporadas[0]}`);
    await page.getByRole('button', { name: 'Continuar' }).click();   // passo 1 (conferência)
    await page.getByRole('button', { name: 'Continuar' }).click();   // passo 2 (ficha)
    const cat = page.locator('.cat', { hasText: categorias.master });
    await expect(cat).toContainText('já pedida');
    await expect(cat.getByRole('checkbox')).toBeDisabled();
    await expect(cat).toContainText(`#${hoje.getFullYear()}-0001`);
  });

  await test.step('quem já se inscreveu antes: dados só para leitura, ficha preenchida e protocolo próprio da temporada', async () => {
    await page.goto(`/inscricao/${temporadas[1]}`);
    await expect(page.getByText('Encontramos o seu cadastro')).toBeVisible();
    await expect(page.getByLabel('Nome completo')).toHaveAttribute('readonly', '');
    await expect(page.getByLabel('CPF')).toHaveValue(`***.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-**`);
    await expect(page.getByLabel('Telefone / WhatsApp')).toHaveValue('(71) 98888-7777');
    await page.getByLabel('Telefone / WhatsApp').fill('71977776666');   // o telefone é o único dado editável
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByLabel('Altura (cm)')).toHaveValue('182');    // ficha da última inscrição
    await expect(page.getByLabel('Peso (kg)')).toHaveValue('84');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await page.locator('.cat', { hasText: categorias.master }).getByRole('checkbox').check();
    await page.getByRole('button', { name: 'Continuar' }).click();
    await page.getByRole('checkbox').first().check();
    await page.getByRole('checkbox').nth(1).check();
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('(71) 97777-6666')).toBeVisible();
    await page.getByRole('button', { name: 'Enviar inscrição' }).click();
    await expect(page.getByTestId('protocolo')).toHaveText(new RegExp(`Protocolo #${hoje.getFullYear()}-0001`));
  });

  await test.step('CPF de outro cadastro (com outro e-mail) é bloqueado sem mostrar dados', async () => {
    const outroCpf = cpfValido((Date.now() + 7) % 80_000_000);
    await chamar(request, token, 'POST', '/api/inscricoes', {
      temporadaId: temporadas[0], nome: 'Dono Do Cadastro', cpf: outroCpf, nascimento: '1970-01-02', sexo: 'Masculino', email: `dono.${sufixo}@exemplo.com`,
      consentimentoLgpd: true, temporadaCategoriaIds: [dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadas[0]}`)).categorias[0].id],
    });
    await conectar(page, await contaConfirmada(request, `intruso.${sufixo}@exemplo.com`, 'Intruso Teste'));
    await page.goto(`/inscricao/${temporadas[1]}`);
    await page.getByLabel('CPF').fill(mascara(outroCpf));
    await page.getByLabel('Data de nascimento').fill('02/01/1970');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Não conseguimos continuar a inscrição online.')).toBeVisible();
    await expect(page.getByText('Dono Do Cadastro')).toHaveCount(0);
    await expect(page.locator('body')).not.toContainText(`dono.${sufixo}`);
    await page.screenshot({ path: 'test-results/i3-bloqueado.png' });
  });

  await test.step('menor de 18 anos é orientado a falar com a diretoria', async () => {
    await conectar(page, await contaConfirmada(request, `jovem.${sufixo}@exemplo.com`, 'Jovem Atleta'));
    await page.goto(`/inscricao/${temporadas[1]}`);
    await page.getByLabel('CPF').fill(mascara(cpfValido((Date.now() + 13) % 80_000_000)));
    await page.getByLabel('Data de nascimento').fill(`10/05/${hoje.getFullYear() - 15}`);
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Inscrição de menor de 18 anos')).toBeVisible();
  });

  await test.step('sem conta o assistente leva a entrar ou criar conta, e no celular cabe na tela', async () => {
    await page.evaluate(() => localStorage.removeItem('sismaster.site.sessao'));
    await page.goto(`/inscricao/${temporadas[0]}`);
    await expect(page.getByRole('link', { name: 'Entrar ou criar conta' })).toBeVisible();
    await conectar(page, await contaConfirmada(request, `celular.${sufixo}@exemplo.com`, 'Pessoa Celular'));
    await page.setViewportSize({ width: 393, height: 851 });
    await page.goto(`/inscricao/${temporadas[0]}`);
    await expect(page.getByRole('heading', { name: 'Seus dados' })).toBeVisible();
    const w = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(w.doc).toBeLessThanOrEqual(w.janela);
  });
});

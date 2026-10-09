import { readFileSync } from 'node:fs';
import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * G11 — agenda dos jogos: conflito de equipe bloqueia o salvar na tela, o horário ajustado salva, e a tabela sai em CSV
 * e em página para imprimir (com filtro de status). Os dados são montados pela API; a tela é conferida no navegador.
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

test('G11: conflito bloqueia, horário ajustado salva, e a tabela sai em CSV e para impressão', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let temporadaId = '';
  let jogos: { id: string; numero: number }[] = [];
  let localId = '';

  await test.step('monta temporada com 3 equipes e a tabela de jogos pela API; agenda o jogo 1', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação G11 ${sufixo}`, sigla: 'G11', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação G11 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/locais`, { nome: 'Ginásio E2E', cidade: 'Salvador', estado: 'BA' });
    localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/locais`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato G11' });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const nome of ['Águias', 'Ursos', 'Lobos'])
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId: tc, nome, cor: '#ff0000' });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, { temporadaId, temporadaCategoriaId: tc, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1 });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    jogos = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`)).jogos as any[])
      .map(j => ({ id: j.id as string, numero: j.numero as number })).sort((a, b) => a.numero - b.numero);
    expect(jogos).toHaveLength(3);
    await chamar(request, token, 'PATCH', `/api/jogos/${jogos[0].id}`, { data: `${ano}-11-08`, hora: '10:00:00', localId });
  });

  await test.step('entra e abre os jogos da temporada', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/temporadas/${temporadaId}/jogos`);
    await expect(page.getByRole('heading', { name: `Jogos — Temporada ${ano}` })).toBeVisible();
  });

  await test.step('a mesma equipe 30 min depois: a tela bloqueia o salvar e diz o motivo', async () => {
    await page.getByRole('row').filter({ hasText: '#2' }).getByRole('button', { name: 'Agendar' }).click();
    await page.getByLabel('Data', { exact: true }).fill(`08/11/${ano}`);
    await page.getByLabel('Hora', { exact: true }).fill('10:30');
    await page.getByLabel('Local', { exact: true }).selectOption({ label: 'Ginásio E2E — Salvador/BA' });
    await expect(page.getByText('Não é possível salvar:')).toBeVisible();
    await expect(page.getByText(/já joga às 10:00/).first()).toBeVisible();
    await expect(page.getByText(/Local ocupado/)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Salvar', exact: true })).toBeDisabled();
    await page.screenshot({ path: 'test-results/g11-conflito.png' });
  });

  await test.step('90 minutos depois não há conflito e o jogo é agendado', async () => {
    await page.getByLabel('Hora', { exact: true }).fill('11:30');
    await expect(page.getByText('Não é possível salvar:')).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Salvar', exact: true })).toBeEnabled();
    await page.getByRole('button', { name: 'Salvar', exact: true }).click();
    await expect(page.getByText('Jogo agendado.')).toBeVisible();
  });

  await test.step('exporta CSV só dos jogos agendados, sem dado pessoal', async () => {
    await page.getByRole('button', { name: 'Imprimir / exportar…' }).click();
    await expect(page.getByText('2 jogos no resultado.')).toBeVisible(); // o jogo 3 ainda está sem data
    await page.getByLabel('Incluir jogos sem data').check();
    await expect(page.getByText('3 jogos no resultado.')).toBeVisible();
    await page.getByLabel('Incluir jogos sem data').uncheck();
    await page.getByLabel('Status do jogo').selectOption('Encerrado');
    await expect(page.getByText('0 jogos no resultado.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Gerar' })).toBeDisabled();
    await page.getByLabel('Status do jogo').selectOption('Agendado');
    await expect(page.getByText('2 jogos no resultado.')).toBeVisible();
    await page.getByLabel(/CSV \(planilha\)/).check();
    const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: 'Gerar' }).click()]);
    expect(download.suggestedFilename()).toBe(`tabela-jogos-${ano}.csv`);
    const csv = readFileSync(await download.path(), 'utf8');
    expect(csv).toContain('"Nº";"Data";"Hora";"Local"');
    expect(csv).toContain('"Ginásio E2E — Salvador/BA"');
    expect(csv).toContain(`"08/11/${ano}";"10:00"`);
    expect(csv.split('\r\n')).toHaveLength(3); // cabeçalho + 2 jogos datados
    await expect(page.getByText('CSV gerado com 2 jogos.')).toBeVisible();
  });

  await test.step('gera a página para imprimir', async () => {
    await page.getByRole('button', { name: 'Imprimir / exportar…' }).click();
    await page.getByRole('button', { name: 'Gerar' }).click();
    const papel = page.locator('#papel-impressao');
    await expect(papel.getByRole('heading', { level: 2 })).toHaveText(`Campeonato G11 — Temporada ${ano}`);
    await expect(papel.getByText(/Ginásio E2E/).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Imprimir', exact: true })).toBeVisible();
    await page.screenshot({ path: 'test-results/g11-impressao.png', fullPage: true });
  });

  await test.step('no papel sai só a folha: o resto da tela e os botões ficam de fora', async () => {
    await page.emulateMedia({ media: 'print' });
    await expect(page.locator('#papel-impressao')).toBeVisible();
    await expect(page.getByRole('heading', { name: `Jogos — Temporada ${ano}` })).toBeHidden();
    await expect(page.getByRole('button', { name: 'Imprimir', exact: true })).toBeHidden();
    await expect(page.getByRole('button', { name: 'Imprimir / exportar…' })).toBeHidden();
    await page.screenshot({ path: 'test-results/g11-impressao-papel.png' });
    await page.emulateMedia({ media: 'screen' });
  });
});

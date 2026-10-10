import { test, expect, devices, type APIRequestContext } from '@playwright/test';

/**
 * A1 — Jogos do dia (área do árbitro) no celular: o jogo de hoje aparece em segundos, os botões são grandes o bastante para o
 * toque e nada estoura a largura da tela. Os dados são montados pela API; a tela é conferida num Pixel 5 (393 px).
 */

test.use({ ...devices['Pixel 5'] });

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

const hojeIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

test('A1: o árbitro acha o jogo de hoje no celular e consegue tocar nos botões', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  const jogos: string[] = [];

  await test.step('monta a temporada com 3 equipes e agenda 2 jogos para hoje pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A1 ${sufixo}`, sigla: 'A1', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A1 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/locais`, { nome: 'Ginásio Municipal de Feira de Santana', cidade: 'Feira de Santana', estado: 'BA' });
    const localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/locais`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A1' });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    const temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const nome of ['Águias do Recôncavo', 'Ursos da Chapada', 'Lobos do Sertão'])
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId: tc, nome, cor: '#ff0000' });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, { temporadaId, temporadaCategoriaId: tc, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1 });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    const lista = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`)).jogos as any[]).sort((a, b) => a.numero - b.numero);
    for (const [i, hora] of ['08:30:00', '10:00:00'].entries()) {
      await chamar(request, token, 'PATCH', `/api/jogos/${lista[i].id}`, { data: hojeIso(), hora, localId });
      jogos.push(lista[i].id);
    }
  });

  await test.step('entra pela área do árbitro e vê os jogos de hoje', async () => {
    await page.goto('/arbitros/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { name: 'Jogos do dia' })).toBeVisible();
    // O administrador enxerga todas as associações (os jogos dos outros testes também aparecem): conferimos os deles.
    await expect(page.getByText(/\d+ jogo\(s\) hoje/)).toBeVisible();
    await expect(page.getByText('📍 Ginásio Municipal de Feira de Santana — Feira de Santana/BA')).toBeVisible();
    await expect(page.getByText('Águias do Recôncavo').first()).toBeVisible();
    for (const id of jogos) await expect(page.locator(`a[href$="/jogos/${id}/sumula"]`)).toBeVisible();
  });

  await test.step('no celular nada estoura a largura da tela', async () => {
    const larguras = await page.evaluate(() => ({ pagina: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(larguras.pagina, `a página tem ${larguras.pagina}px numa janela de ${larguras.janela}px`).toBeLessThanOrEqual(larguras.janela);
    await page.screenshot({ path: 'test-results/a1-celular.png', fullPage: true });
  });

  await test.step('os botões de ação têm tamanho de toque (pelo menos 40 px de altura)', async () => {
    for (const id of jogos) {
      const caixa = await page.locator(`a[href$="/jogos/${id}/sumula"]`).boundingBox();
      expect(caixa!.height, 'altura do botão Preparar súmula').toBeGreaterThanOrEqual(40);
      expect(caixa!.width).toBeGreaterThan(100);
    }
    const filtros = page.getByLabel('Filtrar por categoria');
    expect((await filtros.boundingBox())!.height).toBeGreaterThanOrEqual(32);
  });

  await test.step('tocar em Preparar súmula leva à súmula do jogo', async () => {
    await page.locator(`a[href$="/jogos/${jogos[0]}/sumula"]`).click();
    await expect(page).toHaveURL(new RegExp(`/arbitros/jogos/${jogos[0]}/sumula`));
  });
});

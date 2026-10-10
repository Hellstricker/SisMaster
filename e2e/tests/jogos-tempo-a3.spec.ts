import { test, expect, devices, type APIRequestContext, type WebSocketRoute } from '@playwright/test';

/**
 * A3 — Mesário de tempo com a rede caindo: o aviso de "sem conexão" aparece na hora e trava os botões, a ação feita sem
 * rede é recusada (e não finge que foi), a conexão volta sozinha mesmo depois de uma queda mais longa que o limite
 * antigo do SignalR (~40 s), o estado do servidor (inclusive o que outro mesário fez no meio) volta ao reconectar, recarregar
 * a página não perde nada, e um aparelho com a hora errada não adianta nem atrasa o cronômetro.
 * A queda é simulada derrubando o WebSocket da página e recusando as reconexões. Dados pela API; a tela num Pixel 5.
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

function cpfValido(n: number): string {
  const base = String(100000000 + n).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => { const s = a.reduce((t, x, i) => t + x * (a.length + 1 - i), 0); const r = (s * 10) % 11; return r === 10 ? 0 : r; };
  const d1 = dv(base);
  return [...base, d1, dv([...base, d1])].join('');
}

const segundosDoRelogio = (texto: string) => { const [m, s] = texto.trim().split(':').map(Number); return m * 60 + s; };

test('A3: a mesa de tempo sobrevive à queda de rede, volta sozinha e não se perde no relógio', async ({ page, browser, request }) => {
  test.setTimeout(240_000);
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';
  let partidaId = '';

  await test.step('monta o jogo com a súmula iniciada (4 atletas por equipe) pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A3 ${sufixo}`, sigla: 'A3', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A3 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A3' });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    const temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa: 100 });
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const nome of ['Águias', 'Ursos'])
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId: tc, nome, cor: '#ff0000' });
    const equipes = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/equipes`)).categorias[0].equipes as any[];
    let n = Date.now() % 80_000_000;
    for (const equipe of equipes) {
      for (let i = 0; i < 4; i++) {
        const nome = `${equipe.nome} Atleta ${i + 1}`;
        await chamar(request, token, 'POST', '/api/inscricoes', {
          temporadaId, nome, cpf: cpfValido(n++), nascimento: '1975-05-10', sexo: 'Masculino', email: `a3.${sufixo}.${n}@teste.com`,
          telefone: '71999990000', consentimentoLgpd: true, temporadaCategoriaIds: [tc],
        });
        const ficha = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[]).find(f => f.pessoa.nome === nome);
        const pedido = ficha.categorias[0].id;
        await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${pedido}/aprovar`, {});
        await chamar(request, token, 'POST', `/api/inscricoes/${ficha.id}/pagamentos/taxa`, { dataPagamento: hoje.toISOString().slice(0, 10) });
        await chamar(request, token, 'POST', `/api/equipes/${equipe.id}/atletas`, { inscricaoCategoriaId: pedido });
      }
    }
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, { temporadaId, temporadaCategoriaId: tc, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1 });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    jogoId = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`)).jogos as any[])[0].id;

    await chamar(request, token, 'POST', `/api/jogos/${jogoId}/sumula`);
    const preparo = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}/sumula`));
    for (const time of preparo.times as any[]) {
      const atletas = time.elenco as any[];
      await chamar(request, token, 'PUT', `/api/jogos/${jogoId}/sumula/relacao`, {
        lado: time.lado === 'Casa' ? 0 : 1, capitaoAtletaId: atletas[0].atletaId,
        jogadores: atletas.map((a, i) => ({ atletaId: a.atletaId, numero: String(i + 4), titular: true })),
      });
    }
    await chamar(request, token, 'POST', `/api/jogos/${jogoId}/sumula/iniciar`);
    partidaId = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}/sumula`)).sumula.id;
  });

  // A biblioteca de tempo real vem do nosso servidor: com o CDN público inalcançável a mesa abre do mesmo jeito.
  await page.route(/cdnjs\.cloudflare\.com/, rota => rota.abort());

  // A rede da página: deixa o WebSocket passar, ou derruba (e recusa reconexões) quando "caiu".
  let caiu = false;
  const conexoes: WebSocketRoute[] = [];
  await page.routeWebSocket(/\/hubs\/sumula/, ws => {
    if (caiu) { ws.close(); return; }
    ws.connectToServer();
    conexoes.push(ws);
  });
  const derrubarRede = async () => { caiu = true; for (const ws of conexoes.splice(0)) await ws.close(); };

  await test.step('entra e abre o mesário de tempo: conectado e com o relógio do servidor', async () => {
    await page.goto('/arbitros/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { name: 'Jogos do dia' })).toBeVisible();
    await page.goto(`/arbitros/mesario-tempo.html?partidaId=${partidaId}`);
    await expect(page.locator('#connTxt')).toHaveText('Conectado');
    await expect(page.locator('#clockDisplay')).toHaveText('10:00');
    await expect(page.locator('#semConexao')).toBeHidden();
  });

  await test.step('inicia o cronômetro com a rede boa', async () => {
    await page.locator('#btnIniciar').click();
    await expect(page.locator('#btnPausar')).toBeVisible();
  });

  await test.step('a rede cai: o aviso aparece na hora e os botões travam', async () => {
    await derrubarRede();
    await expect(page.locator('#semConexao')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('#connTxt')).toHaveText('Sem conexão');
    await expect(page.locator('body')).toHaveClass(/offline/);
    await page.screenshot({ path: 'test-results/a3-sem-conexao.png' });
  });

  await test.step('uma ação feita sem rede é recusada, não finge que foi enviada', async () => {
    await page.evaluate(() => (window as any).resetarShot(14));
    await expect(page.locator('#toast')).toHaveText('Sem conexão: a ação NÃO foi enviada');
  });

  await test.step('outro mesário pausa o relógio enquanto esta tela está sem rede', async () => {
    const outra = await browser.newContext({ ...devices['Pixel 5'], storageState: await page.context().storageState() });
    const p2 = await outra.newPage();
    await p2.goto(`/arbitros/mesario-tempo.html?partidaId=${partidaId}`);
    await expect(p2.locator('#connTxt')).toHaveText('Conectado');
    await expect(p2.locator('#btnPausar')).toBeVisible();
    await p2.locator('#btnPausar').click();
    await expect(p2.locator('#btnIniciar')).toBeVisible();
    await outra.close();
  });

  await test.step('a queda dura 50 s (mais que o limite antigo de ~40 s) e a tela segue tentando', async () => {
    await page.waitForTimeout(50_000);
    await expect(page.locator('#semConexao')).toBeVisible();
  });

  await test.step('a rede volta: reconecta sozinha e mostra o que o outro mesário fez', async () => {
    caiu = false;
    await expect(page.locator('#connTxt')).toHaveText('Conectado', { timeout: 20_000 });
    await expect(page.locator('#semConexao')).toBeHidden();
    await expect(page.locator('#btnIniciar')).toBeVisible(); // o relógio foi pausado pelo outro mesário
    await expect(page.locator('#btnPausar')).toBeHidden();
    await page.screenshot({ path: 'test-results/a3-reconectado.png' });
  });

  await test.step('depois de reconectar as ações voltam a funcionar', async () => {
    await page.locator('#btnIniciar').click();
    await expect(page.locator('#btnPausar')).toBeVisible();
    await page.locator('#btnPausar').click();
    await expect(page.locator('#btnIniciar')).toBeVisible();
  });

  await test.step('recarregar a página não perde o estado (relógio, período e placar vêm do servidor)', async () => {
    const antes = await page.locator('#clockDisplay').textContent();
    await page.reload();
    await expect(page.locator('#connTxt')).toHaveText('Conectado');
    await expect(page.locator('#clockDisplay')).toHaveText(antes!.trim());
    await expect(page.locator('#periodoNum')).toHaveText('1º');
  });

  await test.step('um aparelho com a hora 10 min adiantada não estraga o cronômetro', async () => {
    const torta = await browser.newContext({ ...devices['Pixel 5'], storageState: await page.context().storageState() });
    await torta.addInitScript(() => {
      const original = Date.now.bind(Date);
      Date.now = () => original() + 10 * 60_000;
    });
    const p3 = await torta.newPage();
    await p3.goto(`/arbitros/mesario-tempo.html?partidaId=${partidaId}`);
    await expect(p3.locator('#connTxt')).toHaveText('Conectado');
    const parado = segundosDoRelogio((await p3.locator('#clockDisplay').textContent())!);
    await p3.locator('#btnIniciar').click();
    await expect(p3.locator('#btnPausar')).toBeVisible();
    await p3.waitForTimeout(3500);
    const andando = segundosDoRelogio((await p3.locator('#clockDisplay').textContent())!);
    expect(parado - andando, `o relógio foi de ${parado}s para ${andando}s em ~3,5 s`).toBeGreaterThanOrEqual(2);
    expect(parado - andando).toBeLessThanOrEqual(6);
    await p3.locator('#btnPausar').click();
    await torta.close();
  });
});

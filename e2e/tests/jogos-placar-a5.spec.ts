import { test, expect, devices, type APIRequestContext, type WebSocketRoute } from '@playwright/test';

/**
 * A5 — Placar da TV (público, sem login): faltas de equipe grandes e nunca acima de 5, aviso de "sem conexão" que some sozinho,
 * e o fim de jogo com o vencedor em destaque. Dados pela API; a queda de rede é simulada derrubando o WebSocket.
 */

test.use({ viewport: { width: 1280, height: 720 } });   // a TV

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

test('A5: placar da TV com faltas, queda de rede e fim de jogo', async ({ page, browser, request }) => {
  test.setTimeout(240_000);
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';
  let partidaId = '';

  await test.step('monta o jogo com a súmula iniciada (4 atletas por equipe) pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A5 ${sufixo}`, sigla: 'A5', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A5 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A5' });
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
          temporadaId, nome, cpf: cpfValido(n++), nascimento: '1975-05-10', sexo: 'Masculino', email: `a5.${sufixo}.${n}@teste.com`,
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

  // A biblioteca de tempo real vem do nosso servidor: com o CDN público inalcançável a placar abre do mesmo jeito.
  await page.route(/cdnjs\.cloudflare\.com/, rota => rota.abort());

  let caiu = false;
  const conexoes: WebSocketRoute[] = [];
  await page.routeWebSocket(/\/hubs\/sumula/, ws => {
    if (caiu) { ws.close(); return; }
    ws.connectToServer();
    conexoes.push(ws);
  });
  const derrubarRede = async () => { caiu = true; for (const ws of conexoes.splice(0)) await ws.close(); };

  const mesa = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/jogadores`));
  const aguiasEmCasa = (mesa.timeCasa.jogadores as any[]).some(j => j.nome.includes('Águias'));
  const aguias = (aguiasEmCasa ? mesa.timeCasa : mesa.timeVisitante).jogadores as any[];
  const timeAguias = page.locator(aguiasEmCasa ? '#teamCasa' : '#teamVisit');
  const timeUrsos = page.locator(aguiasEmCasa ? '#teamVisit' : '#teamCasa');
  const faltasAguias = page.locator(aguiasEmCasa ? '#faltasCasaCount' : '#faltasVisitCount');
  const evento = (jogador: any, tipo: string) =>
    chamar(request, token, 'POST', `/api/sumulas/${partidaId}/eventos`, { eventoId: crypto.randomUUID(), jogadorId: jogador.id, tipo, tempoJogoSegundos: 500 });

  await test.step('o placar é público: abre sem login, ao vivo, com a faixa de rede escondida', async () => {
    await page.goto(`/arbitros/placar.html?partidaId=${partidaId}`);
    await expect(page.locator('#statusText')).toHaveText('Ao Vivo');
    await expect(page.locator('.faixa-offline')).toBeHidden();
    await expect(page.locator('#gameClock')).toHaveText('10:00');
  });

  await test.step('as faltas de equipe aparecem grandes, sem rótulo, e o placar sobe', async () => {
    await evento(aguias[0], 'Ponto2');
    await evento(aguias[0], 'FaltaPessoal');
    await expect(faltasAguias).toHaveText('1');
    await expect(timeAguias.locator('.team-score')).toHaveText('2');
    const px = await page.locator(".foul-count").first().evaluate(el => parseFloat(getComputedStyle(el).fontSize));
    expect(px, "o número de faltas precisa ser grande para ler de longe").toBeGreaterThan(40);
  });

  await test.step('a equipe passa de 5 faltas: o número fica em 5 e em vermelho', async () => {
    for (const j of [aguias[1], aguias[1], aguias[2], aguias[2], aguias[3], aguias[3]]) await evento(j, 'FaltaPessoal');   // 7 no período
    await expect(faltasAguias).toHaveText('5');
    await expect(faltasAguias).toHaveClass(/estourou/);
    await expect(timeAguias.locator('.foul-dot.estourou')).toHaveCount(5);
    await page.screenshot({ path: 'test-results/a5-faltas.png' });
  });

  await test.step('as faltas zeram quando o período muda', async () => {
    // o mesário de tempo avança o período pelo hub
    await page.evaluate(async ([id, tk]) => {
      const sr = (window as any).signalR;
      const con = new sr.HubConnectionBuilder().withUrl(`/hubs/sumula?partidaId=${id}`, {
        skipNegotiation: true, transport: sr.HttpTransportType.WebSockets, accessTokenFactory: () => tk,
      }).build();
      await con.start(); await con.invoke('AvancarPeriodo', id); await con.stop();
    }, [partidaId, token]);
    await expect(faltasAguias).toHaveText('0');
    await expect(faltasAguias).not.toHaveClass(/estourou/);
    await expect(page.locator('#periodoLabel')).toHaveText('2º PERÍODO');
  });

  await test.step('sem rede: faixa grande, placar escurecido, e volta sozinho', async () => {
    await derrubarRede();
    await expect(page.locator('.faixa-offline')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('#statusText')).toHaveText('Sem conexão');
    await page.screenshot({ path: 'test-results/a5-sem-conexao.png' });
    caiu = false;
    await expect(page.locator('#statusText')).toHaveText('Ao Vivo', { timeout: 20_000 });
    await expect(page.locator('.faixa-offline')).toBeHidden();
  });

  await test.step('encerrar o jogo: FIM DE JOGO, vencedor em destaque e perdedor esmaecido', async () => {
    await chamar(request, token, 'POST', `/api/sumulas/${partidaId}/encerrar`, { confirmarAntesDoTempo: true });
    await expect(page.locator('body')).toHaveClass(/fim/);
    await expect(page.locator('.fim-banner')).toBeVisible();
    await expect(timeAguias).toHaveClass(/venceu/);
    await expect(timeUrsos).toHaveClass(/perdeu/);
    await expect(page.locator('#statusPartida')).toHaveText('Encerrada');
    await page.screenshot({ path: 'test-results/a5-fim.png' });
  });

  await test.step('quem abre o placar depois do fim já vê o jogo encerrado', async () => {
    await page.reload();
    await expect(page.locator('body')).toHaveClass(/fim/);
    await expect(timeAguias).toHaveClass(/venceu/);
  });
});

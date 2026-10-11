import { test, expect, devices, type APIRequestContext, type WebSocketRoute } from '@playwright/test';

/**
 * A4 — Mesário de estatísticas: faltas por jogador e por período (nunca mostra mais de 5), bloqueio do jogador com 5 faltas,
 * fila de lançamentos que sobrevive à queda de rede e à recarga da página, e o tempo de jogo gravado em cada evento.
 * A queda é simulada derrubando o WebSocket. Dados pela API; a tela num Pixel 5.
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

test('A4: faltas, bloqueio na 5ª falta e fila de lançamentos sem rede', async ({ page, browser, request }) => {
  test.setTimeout(240_000);
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';
  let partidaId = '';

  await test.step('monta o jogo com a súmula iniciada (4 atletas por equipe) pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A4 ${sufixo}`, sigla: 'A4', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A4 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A4' });
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
          temporadaId, nome, cpf: cpfValido(n++), nascimento: '1975-05-10', sexo: 'Masculino', email: `a4.${sufixo}.${n}@teste.com`,
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

  let caiu = false;
  const conexoes: WebSocketRoute[] = [];
  await page.routeWebSocket(/\/hubs\/sumula/, ws => {
    if (caiu) { ws.close(); return; }
    ws.connectToServer();
    conexoes.push(ws);
  });
  const derrubarRede = async () => { caiu = true; for (const ws of conexoes.splice(0)) await ws.close(); };

  // Seleciona a ação, o jogador e confirma, como o mesário faz com o dedo.
  const lancar = async (acao: string, jogador: string) => {
    if (await page.locator('#main.panel-open').count()) await page.locator('.panel-close').click();   // no celular o painel aberto cobre os botões
    await page.locator('.action-btn', { hasText: acao }).first().click();
    await page.locator('.player-item', { hasText: jogador }).click();
    await page.locator('#confirmStrip .btn-confirm').click();
  };
  // O sorteio do mando de campo é da tabela: descobre de que lado a equipe Águias está.
  const jogadoresDaMesa = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/jogadores`));
  const aguiasEmCasa = (jogadoresDaMesa.timeCasa.jogadores as any[]).some(j => j.nome.includes('Águias'));
  const faltasAguias = page.locator(aguiasEmCasa ? '#faltasCasa' : '#faltasVisit');
  const placarAguias = page.locator(aguiasEmCasa ? '#placarCasa' : '#placarVisit');
  const linha = (jogador: string) => page.locator('.player-item', { hasText: jogador });

  await test.step('entra e abre o mesário de estatísticas', async () => {
    await page.goto('/arbitros/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { name: 'Jogos do dia' })).toBeVisible();
    await page.goto(`/arbitros/mesario-stats.html?partidaId=${partidaId}`);
    await expect(page.locator('#connTxt')).toHaveText('Conectado');
    await expect(linha('Águias Atleta 1')).toContainText('0 faltas');
  });

  await test.step('cada falta aparece no jogador e na equipe (por período)', async () => {
    await lancar('Falta Pessoal', 'Águias Atleta 1');
    await expect(linha('Águias Atleta 1')).toContainText('1 falta');
    await expect(faltasAguias).toHaveText('Faltas no período: 1');
    for (let i = 2; i <= 4; i++) {
      await lancar('Falta Pessoal', 'Águias Atleta 1');
      await expect(linha('Águias Atleta 1')).toContainText(`${i} faltas`);
    }
    await expect(linha('Águias Atleta 1').locator('.falt')).toHaveClass(/f4/);
  });

  await test.step('a 5ª falta tira o jogador do jogo: FORA, não aceita lançamentos', async () => {
    await lancar('Falta Técnica', 'Águias Atleta 1');
    await expect(linha('Águias Atleta 1')).toContainText('5 faltas');
    await expect(linha('Águias Atleta 1').locator('.tag-fora')).toBeVisible();
    await expect(linha('Águias Atleta 1')).toHaveClass(/fora/);
    await page.locator('.action-btn', { hasText: 'Lance Livre' }).first().click();
    await linha('Águias Atleta 1').click();
    await expect(page.locator('#toast')).toContainText('5 faltas');
    await expect(page.locator('#confirmStrip')).toBeHidden();
    await page.screenshot({ path: 'test-results/a4-quinta-falta.png' });
  });

  await test.step('faltas de equipe: vermelho a partir da 5ª e o número nunca passa de 5', async () => {
    await expect(faltasAguias).toHaveText('Faltas no período: 5');
    await expect(faltasAguias).toHaveClass(/estourou/);
    await lancar('Falta Pessoal', 'Águias Atleta 2');
    await lancar('Falta Pessoal', 'Águias Atleta 2');
    await expect(linha('Águias Atleta 2')).toContainText('2 faltas');
    await expect(faltasAguias).toHaveText('Faltas no período: 5');   // 7 de verdade, mostra 5
  });

  await test.step('o servidor também recusa lançamento de quem tem 5 faltas', async () => {
    const aguias = (aguiasEmCasa ? jogadoresDaMesa.timeCasa : jogadoresDaMesa.timeVisitante).jogadores as any[];
    const jogador = aguias.find(j => j.nome.includes('Águias Atleta 1'));
    const r = await request.post(`/api/sumulas/${partidaId}/eventos`, {
      data: { jogadorId: jogador.id, tipo: 'Ponto2', tempoJogoSegundos: 100 }, headers: { Authorization: `Bearer ${token}` },
    });
    expect(r.status()).toBe(400);
    expect(await r.text()).toContain('5 faltas');
  });

  await test.step('sem rede: aviso, o lançamento vai para a fila e a substituição exige rede', async () => {
    await derrubarRede();
    await expect(page.locator('#semConexao')).toBeVisible({ timeout: 5000 });
    await lancar('Bandeja', 'Águias Atleta 3');
    await expect(page.locator('#filaAviso')).toBeVisible();
    await expect(page.locator('#nFila')).toHaveText('1');
    await page.locator('.action-btn', { hasText: 'Substituição' }).click();
    await expect(page.locator('#toast')).toContainText('substituição precisa da rede');
    await expect(page.locator('#subOverlay')).toBeHidden();
    await page.screenshot({ path: 'test-results/a4-fila-sem-rede.png' });
  });

  await test.step('recarregar a página não perde a fila', async () => {
    await page.reload();
    await expect(page.locator('#nFila')).toHaveText('1');
    await expect(page.locator('#filaAviso')).toBeVisible();
  });

  await test.step('a rede volta: a fila é enviada sozinha e o placar sobe', async () => {
    caiu = false;
    await expect(page.locator('#connTxt')).toHaveText('Conectado', { timeout: 20_000 });
    await expect(page.locator('#filaAviso')).toBeHidden({ timeout: 15_000 });
    await expect(placarAguias).toHaveText('2');
  });

  await test.step('os lançamentos gravaram o tempo de jogo (não mais 0)', async () => {
    const sumula = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/sumula`));
    const eventos = sumula.eventos as any[];
    expect(eventos.length).toBe(8);
    for (const e of eventos) expect(e.tempoJogoSegundos, `evento ${e.tipo}`).toBe(600);
  });
});

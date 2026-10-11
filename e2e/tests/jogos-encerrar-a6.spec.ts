import { test, expect, devices, type APIRequestContext, type WebSocketRoute } from '@playwright/test';

/**
 * A6 — Encerrar e reabrir a súmula pela área do árbitro: empate bloqueia, encerrar antes do tempo exige confirmação, o jogo encerrado
 * de hoje aparece em "Encerrados hoje" e a súmula é reaberta com motivo (o jogo volta a andamento, o placar sai e dá para corrigir e encerrar de novo).
 */

test.use({ ...devices['Pixel 5'] });
const hojeIso = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };

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

test('A6: encerrar com regras e reabrir a súmula com motivo', async ({ page, browser, request }) => {
  test.setTimeout(240_000);
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';
  let partidaId = '';

  await test.step('monta o jogo com a súmula iniciada (4 atletas por equipe) pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A6 ${sufixo}`, sigla: 'A6', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A6 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A6' });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    const temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa: 100 });
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const nome of [`Águias ${sufixo}`, `Ursos ${sufixo}`])
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId: tc, nome, cor: '#ff0000' });
    const equipes = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/equipes`)).categorias[0].equipes as any[];
    let n = Date.now() % 80_000_000;
    for (const equipe of equipes) {
      for (let i = 0; i < 4; i++) {
        const nome = `${equipe.nome} Atleta ${i + 1}`;
        await chamar(request, token, 'POST', '/api/inscricoes', {
          temporadaId, nome, cpf: cpfValido(n++), nascimento: '1975-05-10', sexo: 'Masculino', email: `a6.${sufixo}.${n}@teste.com`,
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

    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/locais`, { nome: `Ginásio A6 ${sufixo}`, cidade: 'Salvador', estado: 'BA' });
    const localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/locais`)) as any[])[0].id;
    await chamar(request, token, 'PATCH', `/api/jogos/${jogoId}`, { data: hojeIso(), hora: '19:00:00', localId });
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

  // O árbitro vê os jogos de hoje de todos os testes: o cartão deste jogo é achado pelo nome das equipes.
  const cartao = page.locator('div[class*="_jogo_"]').filter({ hasText: `Águias ${sufixo}` });

  await test.step('entra como árbitro: o jogo de hoje aparece em andamento', async () => {
    await page.goto('/arbitros/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { name: 'Jogos do dia' })).toBeVisible();
    await expect(cartao.getByRole('button', { name: 'Encerrar súmula' })).toBeVisible();
  });

  await test.step('empatado: o encerramento é bloqueado e a mensagem manda jogar a prorrogação', async () => {
    await cartao.getByRole('button', { name: 'Encerrar súmula' }).click();
    await expect(page.getByText('Placar empatado: jogue a prorrogação antes de encerrar.')).toBeVisible();
    await expect(page.locator('[class*="modalFooter"]').getByRole('button', { name: 'Encerrar súmula' })).toBeDisabled();
    await page.getByRole('button', { name: 'Cancelar' }).click();
  });

  await test.step('com placar e ainda no 1º período: avisa e só encerra com a confirmação', async () => {
    const mesa = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/jogadores`));
    await chamar(request, token, 'POST', `/api/sumulas/${partidaId}/eventos`, {
      eventoId: crypto.randomUUID(), jogadorId: mesa.timeCasa.jogadores[0].id, tipo: 'Ponto3', tempoJogoSegundos: 500,
    });
    await cartao.getByRole('button', { name: 'Encerrar súmula' }).click();
    await expect(page.getByText('3 × 0')).toBeVisible();
    await expect(page.getByText('1º período · tempo restante 10:00')).toBeVisible();
    const confirmar = page.locator('[class*="modalFooter"]').getByRole('button', { name: 'Encerrar súmula' });
    await expect(confirmar).toBeDisabled();
    await page.getByLabel('Confirmo que o jogo terminou assim mesmo.').check();
    await expect(confirmar).toBeEnabled();
    await page.screenshot({ path: 'test-results/a6-encerrar.png' });
    await confirmar.click();
  });

  await test.step('encerrado: sai da lista de jogos e aparece em "Encerrados hoje" com o botão de reabrir', async () => {
    await expect(cartao.getByRole('button', { name: 'Reabrir súmula…' })).toBeVisible();
    await expect(cartao.getByRole('button', { name: 'Encerrar súmula' })).toHaveCount(0);
    const jogo = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}`));
    expect(jogo.status).toBe('Encerrado');
    expect(jogo.placarCasa + jogo.placarVisitante).toBe(3);
  });

  await test.step('reabrir exige o motivo; com ele o jogo volta a andamento e o placar sai do jogo', async () => {
    await cartao.getByRole('button', { name: 'Reabrir súmula…' }).click();
    const reabrir = page.locator('[class*="modalFooter"]').getByRole('button', { name: 'Reabrir súmula' });
    await expect(reabrir).toBeDisabled();
    await page.getByLabel(/Motivo/).fill('ponto de três lançado na equipe errada');
    await page.screenshot({ path: 'test-results/a6-reabrir.png' });
    await reabrir.click();
    await expect(cartao.getByRole('button', { name: 'Encerrar súmula' })).toBeVisible();
    await expect(cartao.getByRole('button', { name: 'Reabrir súmula…' })).toHaveCount(0);
    const jogo = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}`));
    expect(jogo.status).toBe('EmAndamento');
    expect(jogo.placarCasa).toBeNull();
    expect((jogo.historico as any[]).map(h => h.acao)).toEqual(expect.arrayContaining(['EncerrarSumula', 'ReabrirSumula']));
  });

  await test.step('depois de reabrir dá para corrigir (desfazer o evento) e encerrar de novo', async () => {
    await chamar(request, token, 'DELETE', `/api/sumulas/${partidaId}/eventos/ultimo`);
    const mesa = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/jogadores`));
    await chamar(request, token, 'POST', `/api/sumulas/${partidaId}/eventos`, {
      eventoId: crypto.randomUUID(), jogadorId: mesa.timeVisitante.jogadores[0].id, tipo: 'Ponto2', tempoJogoSegundos: 400,
    });
    await page.reload();
    await cartao.getByRole('button', { name: 'Encerrar súmula' }).click();
    await expect(page.getByText('0 × 2')).toBeVisible();
    await page.getByLabel('Confirmo que o jogo terminou assim mesmo.').check();
    await page.locator('[class*="modalFooter"]').getByRole('button', { name: 'Encerrar súmula' }).click();
    await expect(cartao.getByRole('button', { name: 'Reabrir súmula…' })).toBeVisible();
  });
});

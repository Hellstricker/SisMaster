import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * V1 a V4 — Vitrine pública (sem login): o jogo de hoje, ao vivo, encerrado, a temporada com jogos, classificação e equipes, e a página do jogo
 * com a súmula. A associação do site no ambiente de teste é a de sigla SITEE2E (scripts/lib.ps1); o teste a reaproveita ou cria.
 */

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

test('V1–V4: a vitrine pública acompanha o jogo do início ao fim', async ({ page, browser, request }) => {
  test.setTimeout(240_000);
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';
  let partidaId = '';

  await test.step('monta o jogo com a súmula iniciada (4 atletas por equipe) pela API', async () => {
    const existente = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E');
    if (!existente) await chamar(request, token, 'POST', '/api/associacoes', { nome: 'Associação do Site E2E', sigla: 'SITEE2E', uf: 'BA' });
    const assoc = existente?.id ?? (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === 'Associação do Site E2E').id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: `Master 40+ ${sufixo}`, idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[]).find(c => c.nome === `Master 40+ ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: `Vitrine ${sufixo}` });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos.find((c: any) => c.nome === `Vitrine ${sufixo}`).id;
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

    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/locais`, { nome: `Ginásio Vitrine ${sufixo}`, cidade: 'Salvador', estado: 'BA' });
    const localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/locais`)) as any[]).find(l => l.nome === `Ginásio Vitrine ${sufixo}`).id;
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
    partidaId = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}/sumula`)).sumula.id;
  });

  const sem11digitos = async () => {
    const texto = await page.locator('body').innerText();
    expect(texto, 'a página pública não pode mostrar CPF').not.toMatch(/\d{3}\.?\d{3}\.?\d{3}-?\d{2}/);
  };
  const mesa = dados(await chamar(request, token, 'GET', `/api/sumulas/${partidaId}/jogadores`));
  const aguiasEmCasa = (mesa.timeCasa.jogadores as any[]).some(j => j.nome.includes('Águias'));
  const aguias = (aguiasEmCasa ? mesa.timeCasa : mesa.timeVisitante).jogadores as any[];
  const evento = (jogador: any, tipo: string) =>
    chamar(request, token, 'POST', `/api/sumulas/${partidaId}/eventos`, { eventoId: crypto.randomUUID(), jogadorId: jogador.id, tipo, tempoJogoSegundos: 500 });
  const cartao = () => page.locator('a[class*="jogo-link"]').filter({ hasText: `Águias ${sufixo}` });

  await test.step('o início mostra o jogo de hoje com horário, equipes e local, sem login', async () => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: /Jogos de hoje/ })).toBeVisible();
    await expect(cartao()).toContainText(`Ursos ${sufixo}`);
    await expect(cartao()).toContainText('19:00');
    await expect(cartao()).toContainText(`Ginásio Vitrine ${sufixo}`);
    await page.screenshot({ path: 'test-results/vitrine-inicio-hoje.png', fullPage: true });
    await sem11digitos();
  });

  await test.step('começou o jogo: aparece "Ao vivo agora" com o placar, e o placar sobe sozinho', async () => {
    await chamar(request, token, 'POST', `/api/jogos/${jogoId}/sumula/iniciar`);
    await evento(aguias[0], 'Ponto3');
    await expect(page.getByRole('heading', { name: /Ao vivo agora/ })).toBeVisible({ timeout: 30_000 });
    await expect(cartao()).toContainText('3 × 0', { timeout: 20_000 }).catch(async () => { await expect(cartao()).toContainText('0 × 3'); });
    await evento(aguias[1], 'Ponto2');
    await expect(cartao()).toContainText(/5 × 0|0 × 5/, { timeout: 20_000 });   // a página atualiza a cada 5 s
    await page.screenshot({ path: 'test-results/vitrine-ao-vivo.png', fullPage: true });
  });

  await test.step('o jogo ao vivo: placar, súmula com nome e estatísticas do atleta e o botão do placar de TV', async () => {
    await cartao().click();
    await expect(page).toHaveURL(/\/jogos\//);
    await expect(page.getByText('● AO VIVO')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Súmula' })).toBeVisible();
    const linha = page.locator('tr', { hasText: aguias[0].nome });
    await expect(linha).toContainText('3');   // pontos do atleta
    await expect(page.getByRole('link', { name: 'Abrir placar' })).toHaveAttribute('href', new RegExp(`/arbitros/placar.html\\?partidaId=${partidaId}`));
    await page.screenshot({ path: 'test-results/vitrine-jogo-ao-vivo.png', fullPage: true });
    await sem11digitos();
  });

  await test.step('encerrado: o resultado vai para "Últimos resultados" com o vencedor e a página do jogo mostra as parciais', async () => {
    await chamar(request, token, 'POST', `/api/sumulas/${partidaId}/encerrar`, { confirmarAntesDoTempo: true });
    await expect(page.getByText('Encerrado', { exact: true }).first()).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('table.parciais')).toBeVisible();
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Últimos resultados' })).toBeVisible();
    await expect(cartao()).toContainText(/5 × 0|0 × 5/);
    await expect(page.locator('section[aria-labelledby="vivo"]').filter({ hasText: `Águias ${sufixo}` })).toHaveCount(0);   // este jogo saiu do "ao vivo"
  });

  await test.step('campeonatos: a temporada com abas Jogos, Classificação e Equipes', async () => {
    await page.getByRole('link', { name: 'Ver campeonatos e classificação ›' }).click();
    await expect(page).toHaveURL(/\/campeonatos$/);
    await page.getByRole('link', { name: new RegExp(`Vitrine ${sufixo}`) }).click();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(`Vitrine ${sufixo}`);
    await expect(page.locator('table').getByText(`Águias ${sufixo} × Ursos ${sufixo}`).or(page.locator('table').getByText(`Ursos ${sufixo} × Águias ${sufixo}`))).toBeVisible();
    await page.getByRole('link', { name: 'Classificação' }).click();
    await expect(page.locator('table')).toContainText(`Águias ${sufixo}`);
    await expect(page.locator('tr.cl')).toHaveCount(2);   // pontos corridos sem corte: todas "classificadas"
    await page.screenshot({ path: 'test-results/vitrine-classificacao.png', fullPage: true });
    await page.getByRole('link', { name: 'Equipes' }).click();
    await expect(page.getByText('4 atletas').first()).toBeVisible();
    await sem11digitos();
  });

  await test.step('no celular a página do jogo cabe na tela', async () => {
    await page.setViewportSize({ width: 393, height: 851 });
    await page.goto('/');
    await cartao().click();
    await expect(page.getByRole('heading', { name: 'Súmula' })).toBeVisible();
    const largura = await page.evaluate(() => ({ doc: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(largura.doc).toBeLessThanOrEqual(largura.janela);
  });
});

import { test, expect, devices, type APIRequestContext } from '@playwright/test';

/**
 * A2 — Preparar súmula no modo árbitro, no celular: sem o botão de importar do FIBA, relação (camisa, titulares, capitão),
 * aviso de rodízio com menos de 7 relacionados e iniciar. Dados pela API; a tela é conferida num Pixel 5 (393 px).
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

const hojeIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

test('A2: o árbitro prepara a relação no celular e inicia a súmula', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';

  await test.step('monta 2 equipes com 5 atletas cada e agenda o jogo para hoje pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação A2 ${sufixo}`, sigla: 'A2', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação A2 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/locais`, { nome: 'Ginásio A2', cidade: 'Salvador', estado: 'BA' });
    const localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/locais`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato A2' });
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
      for (let i = 0; i < 5; i++) {
        const nome = `${equipe.nome} Atleta ${i + 1}`;
        await chamar(request, token, 'POST', '/api/inscricoes', {
          temporadaId, nome, cpf: cpfValido(n++), nascimento: '1975-05-10', sexo: 'Masculino', email: `a2.${sufixo}.${n}@teste.com`,
          telefone: '71999990000', consentimentoLgpd: true, temporadaCategoriaIds: [tc],
        });
        const ficha = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[]).find(f => f.pessoa.nome === nome);
        const pedido = ficha.categorias[0].id;
        await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${pedido}/aprovar`, {});
        await chamar(request, token, 'POST', `/api/inscricoes/${ficha.id}/pagamentos/taxa`, { dataPagamento: hojeIso() });
        await chamar(request, token, 'POST', `/api/equipes/${equipe.id}/atletas`, { inscricaoCategoriaId: pedido });
      }
    }

    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, { temporadaId, temporadaCategoriaId: tc, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1 });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    jogoId = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`)).jogos as any[])[0].id;
    await chamar(request, token, 'PATCH', `/api/jogos/${jogoId}`, { data: hojeIso(), hora: '19:00:00', localId });
  });

  await test.step('entra pela área do árbitro e abre a súmula do jogo', async () => {
    await page.goto('/arbitros/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('heading', { name: 'Jogos do dia' })).toBeVisible();
    await page.locator(`a[href$="/jogos/${jogoId}/sumula"]`).click();
    await page.getByRole('button', { name: 'Preparar súmula' }).click();
    await expect(page.getByText('Preparação da súmula iniciada.')).toBeVisible();
  });

  await test.step('no modo árbitro não há importação do FIBA', async () => {
    await expect(page.getByRole('button', { name: /Importar do FIBA LiveStats/ })).toHaveCount(0);
    await expect(page.getByRole('button', { name: '‹ Voltar aos jogos' })).toBeVisible();
  });

  await test.step('relaciona 5 atletas por equipe, com camisa, titulares e capitão', async () => {
    for (const lado of ['Águias', 'Ursos']) {
      for (let i = 1; i <= 5; i++) {
        const atleta = `${lado} Atleta ${i}`;
        await page.getByLabel(`Relacionar ${atleta}`).check();
        await page.getByLabel(`Camisa de ${atleta}`).fill(String(i + 3));
        await page.getByLabel(`${atleta} titular`).check();
      }
      await page.getByLabel(`${lado} Atleta 1 capitão`).check();
    }
    await expect(page.getByText('Tudo pronto para iniciar a súmula.')).toBeVisible();
  });

  await test.step('com menos de 7 relacionados avisa que o rodízio não é cumprido', async () => {
    await expect(page.getByText(/Atenção: equipe com menos de 7 relacionados não cumpre o rodízio/)).toBeVisible();
    await expect(page.getByText(/abaixo de 7 \(rodízio\)/).first()).toBeVisible();
  });

  await test.step('no celular nada estoura a largura e os controles dão para tocar', async () => {
    const larguras = await page.evaluate(() => ({ pagina: document.documentElement.scrollWidth, janela: window.innerWidth }));
    expect(larguras.pagina, `a página tem ${larguras.pagina}px numa janela de ${larguras.janela}px`).toBeLessThanOrEqual(larguras.janela);
    await page.screenshot({ path: 'test-results/a2-celular.png', fullPage: true });
    for (const nome of ['Águias Atleta 1', 'Ursos Atleta 5']) {
      for (const rotulo of [`Relacionar ${nome}`, `${nome} titular`, `${nome} capitão`]) {
        const caixa = await page.getByLabel(rotulo).boundingBox();
        expect(caixa!.width, `${rotulo}: largura`).toBeGreaterThanOrEqual(24);
        expect(caixa!.height, `${rotulo}: altura`).toBeGreaterThanOrEqual(24);
      }
      const camisa = await page.getByLabel(`Camisa de ${nome}`).boundingBox();
      expect(camisa!.height, 'altura do campo da camisa').toBeGreaterThanOrEqual(36);
    }
    for (const botao of ['Salvar', 'Iniciar súmula ›']) {
      const caixa = await page.getByRole('button', { name: botao }).boundingBox();
      expect(caixa!.height, `altura do botão ${botao}`).toBeGreaterThanOrEqual(40);
    }
  });

  await test.step('inicia a súmula e aparecem as telas da mesa', async () => {
    await page.getByRole('button', { name: 'Iniciar súmula ›' }).click();
    await expect(page.getByText('Súmula iniciada.')).toBeVisible();
    await expect(page.getByText('Súmula em andamento.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Mesário — tempo' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Mesário — estatísticas' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Placar' })).toBeVisible();
  });
});

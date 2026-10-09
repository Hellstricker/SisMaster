import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * Teste ponta a ponta mínimo (rede de segurança das etapas seguintes), contra o sistema inteiro no ar
 * (nginx -> API -> SQL Server vazio). Percorre o caminho que a diretoria e a mesa fazem de verdade:
 * associação -> categoria -> local -> campeonato -> temporada -> inscrições -> pagamentos -> equipes -> fase
 * -> tabela de jogos -> agendamento -> súmula -> jogo -> encerramento -> classificação.
 * Os passos de cadastro usam a API pelo mesmo endereço do navegador (/api); as telas são conferidas no navegador.
 */

const sufixo = Date.now().toString(36);
/** CPFs diferentes a cada execução: o sistema reconhece a pessoa pelo CPF, então repetir CPF reaproveitaria cadastros antigos. */
const semente = Date.now() % 90_000_000;

/** CPF válido e distinto por número (os dígitos verificadores são calculados). */
function cpfValido(n: number): string {
  const base = String(100000000 + n).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => {
    const soma = a.reduce((s, x, i) => s + x * (a.length + 1 - i), 0);
    const r = (soma * 10) % 11;
    return r === 10 ? 0 : r;
  };
  const d1 = dv(base);
  const d2 = dv([...base, d1]);
  return [...base, d1, d2].join('');
}

async function chamar(request: APIRequestContext, metodo: string, caminho: string, corpo?: unknown) {
  const r = await request.fetch(caminho, { method: metodo, data: corpo });
  const texto = await r.text();
  expect(r.status(), `${metodo} ${caminho} -> ${r.status()}: ${texto}`).toBeLessThan(300);
  return texto ? JSON.parse(texto) : null;
}

/** Algumas rotas devolvem o objeto direto e outras envelopam em { data }. */
const dados = (j: any) => (j && j.data !== undefined && Object.keys(j).length === 1 ? j.data : j);

const hoje = new Date();
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

test('a diretoria cria uma associação pela interface', async ({ page }) => {
  const nome = `Associação UI ${sufixo}`;
  await page.goto('/admin/');
  await page.getByRole('button', { name: '+ Nova associação' }).click();
  await page.getByPlaceholder('Associação Basketball São Paulo', { exact: true }).fill(nome);
  await page.getByPlaceholder('ABSP', { exact: true }).fill('UI');
  await page.getByPlaceholder('SP', { exact: true }).fill('BA');
  await page.getByRole('button', { name: 'Criar associação' }).click();
  await expect(page.getByText(nome)).toBeVisible();
});

test('da associação à classificação: um jogo completo', async ({ page, request }) => {
  const ano = hoje.getFullYear();
  let associacaoId = '', categoriaId = '', campeonatoId = '', temporadaId = '', temporadaCategoriaId = '';
  let faseId = '', jogoId = '', sumulaId = '';

  await test.step('cadastra associação, categoria, local, campeonato e temporada', async () => {
    await chamar(request, 'POST', '/api/associacoes', { nome: `Associação E2E ${sufixo}`, sigla: 'E2E', uf: 'BA' });
    const associacoes = dados(await chamar(request, 'GET', '/api/associacoes')) as any[];
    associacaoId = associacoes.find(a => a.nome === `Associação E2E ${sufixo}`).id;

    await chamar(request, 'POST', `/api/associacoes/${associacaoId}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    categoriaId = (dados(await chamar(request, 'GET', `/api/associacoes/${associacaoId}/categorias`)) as any[])[0].id;

    await chamar(request, 'POST', `/api/associacoes/${associacaoId}/locais`, { nome: 'Ginásio Central', cidade: 'Salvador', estado: 'BA' });
    await chamar(request, 'POST', `/api/associacoes/${associacaoId}/campeonatos`, { nome: 'Campeonato E2E' });
    campeonatoId = dados(await chamar(request, 'GET', `/api/associacoes/${associacaoId}`)).campeonatos[0].id;

    const ontem = new Date(hoje.getTime() - 86_400_000);
    const em30 = new Date(hoje.getTime() + 30 * 86_400_000);
    await chamar(request, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: ontem.toISOString(), dataFimInscricoes: em30.toISOString(),
    });
    temporadaId = dados(await chamar(request, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    temporadaCategoriaId = dados(await chamar(request, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, 'PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa: 100 });
  });

  await test.step('10 atletas se inscrevem; a diretoria aprova e confirma a taxa de cada um', async () => {
    for (let i = 1; i <= 10; i++) {
      await chamar(request, 'POST', '/api/inscricoes', {
        temporadaId, nome: `Atleta ${i}`, cpf: cpfValido(semente + i), nascimento: `197${i % 10}-05-10`, sexo: 'Masculino',
        email: `atleta${i}.${sufixo}@teste.com`, telefone: `7199999${String(i).padStart(4, '0')}`,
        alturaCm: 180, pesoKg: 80, posicao: 'Ala', possuiPlanoSaude: false, consentimentoLgpd: true,
        temporadaCategoriaIds: [temporadaCategoriaId],
      });
    }
    const fichas = dados(await chamar(request, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    expect(fichas).toHaveLength(10);
    for (const f of fichas) {
      await chamar(request, 'PATCH', `/api/inscricoes-categorias/${f.categorias[0].id}/aprovar`, {});
      await chamar(request, 'POST', `/api/inscricoes/${f.id}/pagamentos/taxa`, { dataPagamento: iso(hoje) });
    }
    const depois = dados(await chamar(request, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    expect(depois.every(f => f.categorias[0].status === 'Efetivada')).toBe(true);
  });

  await test.step('encerra as inscrições e forma duas equipes de 5', async () => {
    await chamar(request, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const [nome, cor] of [['Águias', '#ff0000'], ['Ursos', '#0000ff']]) {
      await chamar(request, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId, nome, cor });
    }
    const cat = dados(await chamar(request, 'GET', `/api/temporadas/${temporadaId}/equipes`)).categorias[0];
    const [aguias, ursos] = ['Águias', 'Ursos'].map(n => cat.equipes.find((e: any) => e.nome === n));
    const livres = cat.semEquipe.map((a: any) => a.inscricaoCategoriaId) as string[];
    expect(livres).toHaveLength(10);
    for (let i = 0; i < 10; i++) {
      await chamar(request, 'POST', `/api/equipes/${(i < 5 ? aguias : ursos).id}/atletas`, { inscricaoCategoriaId: livres[i] });
    }
  });

  await test.step('cria a fase de pontos corridos e gera a tabela de jogos', async () => {
    await chamar(request, 'POST', `/api/temporadas/${temporadaId}/fases`, {
      temporadaId, temporadaCategoriaId, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1,
    });
    await chamar(request, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    const lista = dados(await chamar(request, 'GET', `/api/temporadas/${temporadaId}/jogos`));
    expect(lista.jogos).toHaveLength(1);
    jogoId = lista.jogos[0].id;
    faseId = lista.fases[0].id;
  });

  await test.step('agenda o jogo para hoje e ele aparece para o árbitro', async () => {
    const localId = (dados(await chamar(request, 'GET', `/api/associacoes/${associacaoId}/locais`)) as any[])[0].id;
    await chamar(request, 'PATCH', `/api/jogos/${jogoId}`, { data: iso(hoje), hora: '19:00:00', localId });
    await page.goto('/arbitros/');
    await expect(page.getByText('1 jogo(s) hoje')).toBeVisible();
    await expect(page.getByText('Águias').first()).toBeVisible();
  });

  await test.step('prepara a súmula, relaciona 5 titulares por time e inicia', async () => {
    await chamar(request, 'POST', `/api/jogos/${jogoId}/sumula`, {});
    const preparo = dados(await chamar(request, 'GET', `/api/jogos/${jogoId}/sumula`));
    for (const t of preparo.times) {
      const jogadores = t.elenco.map((a: any, i: number) => ({ atletaId: a.atletaId, numero: String(i + 4), titular: true }));
      await chamar(request, 'PUT', `/api/jogos/${jogoId}/sumula/relacao`, {
        lado: t.lado, tecnico: `Prof. ${t.nome}`, jogadores, capitaoAtletaId: t.elenco[0].atletaId,
      });
    }
    await chamar(request, 'POST', `/api/jogos/${jogoId}/sumula/iniciar`, {});
    sumulaId = dados(await chamar(request, 'GET', `/api/jogos/${jogoId}/sumula`)).sumula.id;
  });

  await test.step('a mesa lança os eventos e o placar confere (6 x 2)', async () => {
    const j = dados(await chamar(request, 'GET', `/api/sumulas/${sumulaId}/jogadores`));
    const casa = j.timeCasa.jogadores[0].id as string;
    const visitante = j.timeVisitante.jogadores[0].id as string;
    for (const [jogadorId, tipo, tempo] of [
      [casa, 'Ponto2', 500], [casa, 'Ponto3', 480], [casa, 'LanceLivre', 470],
      [visitante, 'Ponto2', 400], [casa, 'Assistencia', 500], [visitante, 'FaltaPessoal', 300],
    ] as const) {
      await chamar(request, 'POST', `/api/sumulas/${sumulaId}/eventos`, { jogadorId, tipo, tempoJogoSegundos: tempo });
    }
    const placar = await chamar(request, 'GET', `/api/sumulas/${sumulaId}/placar`);
    expect([placar.placarCasa, placar.placarVisitante]).toEqual([6, 2]);
    expect(placar.faltasVisitante).toBe(1);
  });

  await test.step('encerra a súmula: o jogo encerra e a fase termina', async () => {
    await chamar(request, 'POST', `/api/sumulas/${sumulaId}/encerrar`, {});
    const jogo = await chamar(request, 'GET', `/api/jogos/${jogoId}`);
    expect(jogo.casa.texto).toBe('Águias');
    const classificacao = dados(await chamar(request, 'GET', `/api/fases/${faseId}/classificacao`));
    expect(classificacao.status).toBe('Encerrada');
    const [primeiro, segundo] = classificacao.classificacao.linhas;
    expect([primeiro.equipe.nome, primeiro.total, primeiro.saldo]).toEqual(['Águias', 2, 4]);
    expect([segundo.equipe.nome, segundo.total, segundo.saldo]).toEqual(['Ursos', 1, -4]);
  });

  await test.step('a classificação aparece na tela de gestão', async () => {
    await page.goto(`/admin/fases/${faseId}/classificacao`);
    await expect(page.getByRole('heading', { name: /Classificação — Fase única/ })).toBeVisible();
    await expect(page.getByRole('row', { name: /1º\s+Águias/ })).toContainText('+4');
    await expect(page.getByRole('row', { name: /2º\s+Ursos/ })).toContainText('-4');
  });

  await test.step('o jogo aparece como encerrado e some da lista do árbitro', async () => {
    await page.goto(`/admin/jogos/${jogoId}`);
    await expect(page.getByText('Encerrado').first()).toBeVisible();
    await page.goto('/arbitros/');
    await expect(page.getByText('0 jogo(s) hoje')).toBeVisible();
  });
});

import { test, expect, type APIRequestContext, type Page } from '@playwright/test';

/**
 * Teste ponta a ponta mínimo (rede de segurança das etapas seguintes), contra o sistema inteiro no ar
 * (nginx -> API -> SQL Server vazio). Percorre o caminho que a diretoria e a mesa fazem de verdade:
 * associação -> categoria -> local -> campeonato -> temporada -> inscrições -> pagamentos -> equipes -> fase
 * -> tabela de jogos -> agendamento -> súmula -> jogo -> encerramento -> classificação.
 * Os passos de cadastro usam a API pelo mesmo endereço do navegador (/api); as telas são conferidas no navegador.
 */

const sufixo = Date.now().toString(36);

// Administrador inicial do ambiente de teste (criado pela Identidade a partir do .env do ambiente): nasce com senha temporária.
const EMAIL = process.env.E2E_ADMIN_EMAIL ?? 'admin@teste.local';
const SENHA_INICIAL = process.env.E2E_ADMIN_SENHA_INICIAL ?? 'Teste#Admin2026';
const SENHA_NOVA = 'Admin#E2E2026x';

/** Token do administrador. Se a senha ainda for a temporária, troca (a troca é obrigatória no primeiro acesso). */
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
  throw new Error('Não consegui entrar como administrador de teste (senha temporária nem a nova funcionam).');
}

async function entrarNaInterface(page: Page, senha = SENHA_NOVA) {
  await page.goto('/admin/entrar');
  await page.getByLabel('E-mail').fill(EMAIL);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await page.getByRole('button', { name: 'Entrar' }).click();
}
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

async function chamar(request: APIRequestContext, token: string, metodo: string, caminho: string, corpo?: unknown) {
  const r = await request.fetch(caminho, { method: metodo, data: corpo, headers: { Authorization: `Bearer ${token}` } });
  const texto = await r.text();
  expect(r.status(), `${metodo} ${caminho} -> ${r.status()}: ${texto}`).toBeLessThan(300);
  return texto ? JSON.parse(texto) : null;
}

/** Algumas rotas devolvem o objeto direto e outras envelopam em { data }. */
const dados = (j: any) => (j && j.data !== undefined && Object.keys(j).length === 1 ? j.data : j);

const hoje = new Date();
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

test('sem login, a gestão leva ao login e guarda a página pedida', async ({ page }) => {
  await page.goto('/admin/temporadas/00000000-0000-0000-0000-000000000001');
  await expect(page).toHaveURL(/\/admin\/entrar\?voltar=/);
  await expect(page.getByRole('button', { name: 'Entrar' })).toBeVisible();
});

test('senha errada não entra', async ({ page }) => {
  await page.goto('/admin/entrar');
  await page.getByLabel('E-mail').fill(EMAIL);
  await page.getByLabel('Senha', { exact: true }).fill('Errada12345');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page.getByRole('alert')).toContainText('incorretos');
  await expect(page).toHaveURL(/\/admin\/entrar/);
});

test('primeiro acesso: a senha temporária precisa ser trocada antes de qualquer coisa', async ({ page, request }) => {
  const temporaria = await request.post('/api/identidade/autenticar', { data: { email: EMAIL, password: SENHA_INICIAL } });
  test.skip(!temporaria.ok(), 'a senha temporária deste ambiente já foi trocada em uma execução anterior');

  await entrarNaInterface(page, SENHA_INICIAL);
  await expect(page).toHaveURL(/\/admin\/minha-conta/);
  await expect(page.getByText('Defina uma nova senha para continuar')).toBeVisible();

  // enquanto a senha for a temporária, o resto do sistema fica bloqueado
  await page.goto('/admin/');
  await expect(page).toHaveURL(/\/admin\/minha-conta/);

  await page.getByLabel('Senha temporária recebida').fill(SENHA_INICIAL);
  await page.getByLabel('Nova senha', { exact: true }).fill(SENHA_NOVA);
  await page.getByLabel('Repita a nova senha').fill(SENHA_NOVA);
  await page.getByRole('button', { name: 'Alterar senha' }).click();
  await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
  await expect(page).toHaveURL(/\/admin\/?$/);
});

test('sair encerra a sessão', async ({ page, request }) => {
  await tokenDoAdministrador(request); // garante a senha definitiva
  await entrarNaInterface(page);
  await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
  await page.getByRole('button', { name: 'Menu do usuário' }).click();
  await page.getByRole('button', { name: 'Sair' }).click();
  await expect(page).toHaveURL(/\/admin\/entrar/);
  await page.goto('/admin/');
  await expect(page).toHaveURL(/\/admin\/entrar/);
});

test('a diretoria cria uma associação pela interface', async ({ page, request }) => {
  await tokenDoAdministrador(request);
  await entrarNaInterface(page);
  const nome = `Associação UI ${sufixo}`;
  await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
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
  let token = '';

  await test.step('entra como administrador (pela tela e pela API)', async () => {
    token = await tokenDoAdministrador(request);
    await entrarNaInterface(page);
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
  });

  await test.step('cadastra associação, categoria, local, campeonato e temporada', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação E2E ${sufixo}`, sigla: 'E2E', uf: 'BA' });
    const associacoes = dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[];
    associacaoId = associacoes.find(a => a.nome === `Associação E2E ${sufixo}`).id;

    await chamar(request, token, 'POST', `/api/associacoes/${associacaoId}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${associacaoId}/categorias`)) as any[])[0].id;

    await chamar(request, token, 'POST', `/api/associacoes/${associacaoId}/locais`, { nome: 'Ginásio Central', cidade: 'Salvador', estado: 'BA' });
    await chamar(request, token, 'POST', `/api/associacoes/${associacaoId}/campeonatos`, { nome: 'Campeonato E2E' });
    campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${associacaoId}`)).campeonatos[0].id;

    const ontem = new Date(hoje.getTime() - 86_400_000);
    const em30 = new Date(hoje.getTime() + 30 * 86_400_000);
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: ontem.toISOString(), dataFimInscricoes: em30.toISOString(),
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    temporadaCategoriaId = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa: 100 });
  });

  await test.step('10 atletas se inscrevem; a diretoria aprova e confirma a taxa de cada um', async () => {
    for (let i = 1; i <= 10; i++) {
      await chamar(request, token, 'POST', '/api/inscricoes', {
        temporadaId, nome: `Atleta ${i}`, cpf: cpfValido(semente + i), nascimento: `197${i % 10}-05-10`, sexo: 'Masculino',
        email: `atleta${i}.${sufixo}@teste.com`, telefone: `7199999${String(i).padStart(4, '0')}`,
        alturaCm: 180, pesoKg: 80, posicao: 'Ala', possuiPlanoSaude: false, consentimentoLgpd: true,
        temporadaCategoriaIds: [temporadaCategoriaId],
      });
    }
    const fichas = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    expect(fichas).toHaveLength(10);
    for (const f of fichas) {
      await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${f.categorias[0].id}/aprovar`, {});
      await chamar(request, token, 'POST', `/api/inscricoes/${f.id}/pagamentos/taxa`, { dataPagamento: iso(hoje) });
    }
    const depois = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    expect(depois.every(f => f.categorias[0].status === 'Efetivada')).toBe(true);
  });

  await test.step('encerra as inscrições e forma duas equipes de 5', async () => {
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const [nome, cor] of [['Águias', '#ff0000'], ['Ursos', '#0000ff']]) {
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId, nome, cor });
    }
    const cat = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/equipes`)).categorias[0];
    const [aguias, ursos] = ['Águias', 'Ursos'].map(n => cat.equipes.find((e: any) => e.nome === n));
    const livres = cat.semEquipe.map((a: any) => a.inscricaoCategoriaId) as string[];
    expect(livres).toHaveLength(10);
    for (let i = 0; i < 10; i++) {
      await chamar(request, token, 'POST', `/api/equipes/${(i < 5 ? aguias : ursos).id}/atletas`, { inscricaoCategoriaId: livres[i] });
    }
  });

  await test.step('cria a fase de pontos corridos e gera a tabela de jogos', async () => {
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, {
      temporadaId, temporadaCategoriaId, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1,
    });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    const lista = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`));
    expect(lista.jogos).toHaveLength(1);
    jogoId = lista.jogos[0].id;
    faseId = lista.fases[0].id;
  });

  await test.step('agenda o jogo para hoje e ele aparece para o árbitro', async () => {
    const localId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${associacaoId}/locais`)) as any[])[0].id;
    await chamar(request, token, 'PATCH', `/api/jogos/${jogoId}`, { data: iso(hoje), hora: '19:00:00', localId });
    await page.goto('/arbitros/');
    await expect(page.getByText('1 jogo(s) hoje')).toBeVisible();
    await expect(page.getByText('Águias').first()).toBeVisible();
  });

  await test.step('prepara a súmula, relaciona 5 titulares por time e inicia', async () => {
    await chamar(request, token, 'POST', `/api/jogos/${jogoId}/sumula`, {});
    const preparo = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}/sumula`));
    for (const t of preparo.times) {
      const jogadores = t.elenco.map((a: any, i: number) => ({ atletaId: a.atletaId, numero: String(i + 4), titular: true }));
      await chamar(request, token, 'PUT', `/api/jogos/${jogoId}/sumula/relacao`, {
        lado: t.lado, tecnico: `Prof. ${t.nome}`, jogadores, capitaoAtletaId: t.elenco[0].atletaId,
      });
    }
    await chamar(request, token, 'POST', `/api/jogos/${jogoId}/sumula/iniciar`, {});
    sumulaId = dados(await chamar(request, token, 'GET', `/api/jogos/${jogoId}/sumula`)).sumula.id;
  });

  await test.step('a mesa lança os eventos e o placar confere (6 x 2)', async () => {
    const j = dados(await chamar(request, token, 'GET', `/api/sumulas/${sumulaId}/jogadores`));
    const casa = j.timeCasa.jogadores[0].id as string;
    const visitante = j.timeVisitante.jogadores[0].id as string;
    for (const [jogadorId, tipo, tempo] of [
      [casa, 'Ponto2', 500], [casa, 'Ponto3', 480], [casa, 'LanceLivre', 470],
      [visitante, 'Ponto2', 400], [casa, 'Assistencia', 500], [visitante, 'FaltaPessoal', 300],
    ] as const) {
      await chamar(request, token, 'POST', `/api/sumulas/${sumulaId}/eventos`, { jogadorId, tipo, tempoJogoSegundos: tempo });
    }
    const placar = await chamar(request, token, 'GET', `/api/sumulas/${sumulaId}/placar`);
    expect([placar.placarCasa, placar.placarVisitante]).toEqual([6, 2]);
    expect(placar.faltasVisitante).toBe(1);
  });

  await test.step('encerra a súmula: o jogo encerra e a fase termina', async () => {
    await chamar(request, token, 'POST', `/api/sumulas/${sumulaId}/encerrar`, {});
    const jogo = await chamar(request, token, 'GET', `/api/jogos/${jogoId}`);
    expect(jogo.casa.texto).toBe('Águias');
    const classificacao = dados(await chamar(request, token, 'GET', `/api/fases/${faseId}/classificacao`));
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

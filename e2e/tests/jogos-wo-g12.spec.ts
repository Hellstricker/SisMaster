import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * G12 — W.O. pela tela do jogo: registrar, desfazer (motivo obrigatório), histórico e a volta a Agendado.
 * Os dados são montados pela API; a tela é conferida no navegador.
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

test('G12: registrar W.O., desfazer com motivo e o histórico do jogo', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let jogoId = '';

  await test.step('monta a temporada com 2 equipes e a tabela pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação G12 ${sufixo}`, sigla: 'G12', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação G12 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato G12' });
    const campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    const temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`, {});
    for (const nome of ['Águias', 'Ursos'])
      await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/equipes`, { temporadaId, temporadaCategoriaId: tc, nome, cor: '#ff0000' });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases`, { temporadaId, temporadaCategoriaId: tc, nome: 'Fase única', tipo: 'PontosCorridos', numeroTurnos: 1 });
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, {});
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/tabela-jogos`, {});
    jogoId = (dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/jogos`)).jogos as any[])[0].id;
  });

  await test.step('entra e abre o jogo', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/jogos/${jogoId}`);
    await expect(page.getByRole('heading', { name: /^Jogo #1/ })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Desfazer W.O.…' })).toHaveCount(0);
  });

  await test.step('registra o W.O. da equipe da casa e vê o resultado e o histórico', async () => {
    await page.getByRole('button', { name: 'Registrar W.O.…' }).click();
    await page.getByRole('button', { name: 'Registrar W.O.', exact: true }).click();
    await expect(page.getByText(/W\.O\. registrado\. .* ausente · 20 × 0/)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Histórico do jogo' })).toBeVisible();
    await expect(page.getByText(/W\.O\. registrado: .* ausente; .* vence por 20 × 0/)).toBeVisible();
  });

  await test.step('desfazer exige o motivo', async () => {
    await page.getByRole('button', { name: 'Desfazer W.O.…' }).click();
    await page.getByRole('button', { name: 'Desfazer W.O.', exact: true }).click();
    await expect(page.getByText('Informe o motivo para desfazer o W.O.')).toBeVisible();
    await expect(page.getByText(/W\.O\. registrado\. /)).toBeVisible(); // nada mudou
    await page.screenshot({ path: 'test-results/g12-desfazer.png' });
  });

  await test.step('com o motivo, o jogo volta a Agendado e o histórico guarda os dois fatos', async () => {
    await page.getByLabel(/Motivo/).fill('marquei a equipe errada');
    await page.getByRole('button', { name: 'Desfazer W.O.', exact: true }).click();
    await expect(page.getByText('W.O. desfeito: o jogo voltou a Agendado.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Registrar W.O.…' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Desfazer W.O.…' })).toHaveCount(0);
    await expect(page.getByText(/W\.O\. de .* desfeito; o jogo voltou a Agendado\. Motivo: marquei a equipe errada/)).toBeVisible();
    await expect(page.getByText(/W\.O\. registrado: .* ausente/)).toBeVisible(); // o registro original continua no histórico
    await page.screenshot({ path: 'test-results/g12-historico.png', fullPage: true });
  });
});

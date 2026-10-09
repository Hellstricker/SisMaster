import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * G6 — detalhe da temporada: o ciclo de vida (encerrar inscrições, regras que bloqueiam), valor por categoria,
 * cobranças, travas (remover categoria com fichas, reabrir inscrição com cobrança) e datas opcionais da competição.
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

function cpfValido(n: number): string {
  const base = String(100000000 + n).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => { const s = a.reduce((t, x, i) => t + x * (a.length + 1 - i), 0); const r = (s * 10) % 11; return r === 10 ? 0 : r; };
  const d1 = dv(base);
  return [...base, d1, dv([...base, d1])].join('');
}

test('G6: ciclo da temporada, valores, cobranças e travas', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const token = await tokenDoAdministrador(request);
  let temporadaId = '', campeonatoId = '';

  await test.step('monta associação, categoria e temporada (com datas da competição) pela API', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação G6 ${sufixo}`, sigla: 'G6', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação G6 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato G6' });
    campeonatoId = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonatoId}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
      dataInicioCompeticao: `${ano}-11-01`, dataFimCompeticao: `${ano}-12-20`,
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonatoId}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    const tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'POST', '/api/inscricoes', {
      temporadaId, nome: 'Atleta Teste G6', cpf: cpfValido(Date.now() % 90_000_000), nascimento: '1975-05-10', sexo: 'Masculino',
      email: `g6.${sufixo}@teste.com`, telefone: '71999990000', consentimentoLgpd: true, temporadaCategoriaIds: [tc],
    });
  });

  await test.step('entra e abre o detalhe da temporada', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible(); // login concluído
    await page.goto(`/admin/temporadas/${temporadaId}`);
    await expect(page.getByRole('heading', { name: `Temporada ${ano}` })).toBeVisible();
    await expect(page.getByText(/Competição: 01\/11\/\d{4} – 20\/12\/\d{4}/)).toBeVisible();
    await expect(page.getByText('Antes de avançar')).toBeVisible();
    await expect(page.getByText(/1 pedidos pendentes de análise/)).toBeVisible();
  });

  await test.step('com a ficha, a categoria não pode ser removida e o motivo aparece', async () => {
    const remover = page.getByRole('button', { name: 'Remover' }).first();
    await expect(remover).toBeDisabled();
    await expect(remover).toHaveAttribute('title', /1 fichas/);
  });

  await test.step('encerra as inscrições pelo botão e pede confirmação', async () => {
    await page.getByRole('button', { name: 'Encerrar inscrições', exact: true }).first().click();
    await page.getByRole('button', { name: 'Confirmar', exact: true }).click();
    await expect(page.getByText('Situação alterada: Encerrar inscrições.')).toBeVisible();
    await expect(page.getByText('Inscrições encerradas').first()).toBeVisible();
  });

  await test.step('iniciar fica bloqueado sem a tabela de jogos', async () => {
    await expect(page.getByText('Gere a tabela de jogos (equipes e fases) antes de iniciar.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Iniciar temporada', exact: true })).toBeDisabled();
  });

  await test.step('cobranças exigem o valor da categoria; depois de geradas travam a reabertura', async () => {
    await expect(page.getByRole('button', { name: 'Gerar cobranças', exact: true })).toBeDisabled();
    const valor = page.getByLabel('Valor de Master 40+');
    await valor.fill('500,00');
    await valor.blur();
    await expect(page.getByText('Valor de Master 40+ salvo.')).toBeVisible();

    await page.getByRole('button', { name: 'Gerar cobranças', exact: true }).click();
    await expect(page.getByText(/Cobranças geradas em/)).toBeVisible();
    await expect(page.getByText(/previsto R\$\s*500,00/)).toBeVisible();
    await expect(page.getByRole('switch', { name: 'Habilitar inscrições' }).or(page.getByRole('button', { name: 'Habilitar inscrições' }))).toBeDisabled();
  });

  await test.step('a diretoria ainda inscreve depois das cobranças e a tela avisa para recalcular', async () => {
    await chamar(request, token, 'POST', '/api/inscricoes', {
      temporadaId, nome: 'Outro Atleta G6', cpf: cpfValido((Date.now() + 7) % 90_000_000), nascimento: '1978-03-02', sexo: 'Masculino',
      email: `g6b.${sufixo}@teste.com`, telefone: '71999990001', consentimentoLgpd: true,
      temporadaCategoriaIds: [dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id],
    });
    await page.reload();
    await expect(page.getByText(/1 ficha entrou depois das cobranças/)).toBeVisible();
    await page.screenshot({ path: 'test-results/g6-temporada.png', fullPage: true });
  });

  await test.step('as datas da competição são opcionais: dá para limpar e salvar', async () => {
    await page.getByLabel(/Início da competição/).fill('');
    await page.getByLabel(/Fim da competição/).fill('');
    await page.getByRole('button', { name: 'Salvar datas' }).click();
    await expect(page.getByText('Datas salvas.')).toBeVisible();
    await expect(page.getByText(/Competição:/)).toHaveCount(0);
  });
});

import { test, expect, type APIRequestContext } from '@playwright/test';
import { readFile } from 'node:fs/promises';

/**
 * G7 — Inscrições: estornar pagamento, reabrir pedido recusado, desfazer aprovação, corrigir dados, exportar CSV e
 * importar planilha, tudo pela interface. Os dados de partida são montados pela API.
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
  const base = String(100000000 + (n * 7919) % 800000000).slice(0, 9).split('').map(Number);
  const dv = (a: number[]) => { const s = a.reduce((t, x, i) => t + x * (a.length + 1 - i), 0); const r = (s * 10) % 11; return r === 10 ? 0 : r; };
  const d1 = dv(base);
  return [...base, d1, dv([...base, d1])].join('');
}

test('G7: correções, exportação e importação pela interface', async ({ page, request }) => {
  const hoje = new Date();
  const ano = hoje.getFullYear();
  const semente = Date.now() % 1_000_000;
  const token = await tokenDoAdministrador(request);
  let temporadaId = '', tc = '';
  const nomes = { pago: `Pago Estorno ${sufixo}`, recusado: `Recusado Reabrir ${sufixo}`, pendente: `Pendente Dados ${sufixo}` };

  await test.step('monta temporada com 3 fichas: uma paga, uma recusada e uma pendente', async () => {
    await chamar(request, token, 'POST', '/api/associacoes', { nome: `Associação G7 ${sufixo}`, sigla: 'G7', uf: 'BA' });
    const assoc = (dados(await chamar(request, token, 'GET', '/api/associacoes')) as any[]).find(a => a.nome === `Associação G7 ${sufixo}`).id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/categorias`, {
      nome: 'Master 40+', idadeMinima: 40, sexo: 'Masculino', aceitaAbaixoIdadeMinima: false, minimoPeriodosEmQuadra: 1, minimoPeriodosForaQuadra: 1,
    });
    const categoriaId = (dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}/categorias`)) as any[])[0].id;
    await chamar(request, token, 'POST', `/api/associacoes/${assoc}/campeonatos`, { nome: 'Campeonato G7' });
    const campeonato = dados(await chamar(request, token, 'GET', `/api/associacoes/${assoc}`)).campeonatos[0].id;
    await chamar(request, token, 'POST', `/api/campeonatos/${campeonato}/temporadas`, {
      ano, dataInicioInscricoes: new Date(hoje.getTime() - 86_400_000).toISOString(), dataFimInscricoes: new Date(hoje.getTime() + 30 * 86_400_000).toISOString(),
    });
    temporadaId = dados(await chamar(request, token, 'GET', `/api/campeonatos/${campeonato}`)).temporadas[0].id;
    await chamar(request, token, 'POST', `/api/temporadas/${temporadaId}/categorias`, { categoriaId });
    tc = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}`)).categorias[0].id;
    await chamar(request, token, 'PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa: 100 });

    let i = 0;
    for (const nome of Object.values(nomes)) {
      i++;
      await chamar(request, token, 'POST', '/api/inscricoes', {
        temporadaId, nome, cpf: cpfValido(semente + i), nascimento: '1975-05-10', sexo: 'Masculino', email: `g7${i}.${sufixo}@teste.com`, telefone: '71999990000',
        alturaCm: 178, pesoKg: 80, posicao: 'Ala', consentimentoLgpd: true, temporadaCategoriaIds: [tc],
      });
    }
    const fichas = dados(await chamar(request, token, 'GET', `/api/temporadas/${temporadaId}/inscricoes`)) as any[];
    const de = (n: string) => fichas.find(f => f.pessoa.nome === n);
    await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${de(nomes.pago).categorias[0].id}/aprovar`, {});
    await chamar(request, token, 'POST', `/api/inscricoes/${de(nomes.pago).id}/pagamentos/taxa`, { dataPagamento: hoje.toISOString().slice(0, 10) });
    await chamar(request, token, 'PATCH', `/api/inscricoes-categorias/${de(nomes.recusado).categorias[0].id}/recusar`, { motivo: 'documento ilegível' });
  });

  const linha = (nome: string) => page.getByRole('row').filter({ hasText: nome });

  await test.step('entra e abre as inscrições', async () => {
    await page.goto('/admin/entrar');
    await page.getByLabel('E-mail').fill(EMAIL);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA_NOVA);
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByRole('button', { name: '+ Nova associação' })).toBeVisible();
    await page.goto(`/admin/temporadas/${temporadaId}/inscricoes`);
    await expect(page.getByRole('heading', { name: new RegExp(`Inscrições — Temporada ${ano}`) })).toBeVisible();
    await expect(page.getByText('Pendentes de análise')).toBeVisible();
    await expect(linha(nomes.pago)).toBeVisible();
    await expect(linha(nomes.pago).getByText('Taxa paga')).toBeVisible();
  });

  await test.step('estorna a taxa: exige motivo e o pedido volta a aguardar pagamento', async () => {
    await linha(nomes.pago).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: /Ver \/ estornar pagamentos/ }).click();
    await page.getByRole('button', { name: 'Estornar' }).click();
    await expect(page.getByText('Informe o motivo do estorno antes de estornar.')).toBeVisible();
    await page.getByLabel(/Motivo do estorno/).fill('lançada na ficha errada');
    await page.getByRole('button', { name: 'Estornar' }).click();
    await expect(page.getByText('Pagamento estornado e registrado no histórico.')).toBeVisible();
    await expect(linha(nomes.pago).getByText('Taxa pendente')).toBeVisible();
    await expect(linha(nomes.pago).getByText('Aguardando pagamento')).toBeVisible();
  });

  await test.step('desfaz a aprovação (com motivo) e o pedido volta a pendente', async () => {
    await linha(nomes.pago).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: /Desfazer aprovação/ }).click();
    await page.getByRole('button', { name: 'Desfazer aprovação', exact: true }).click();
    await expect(page.getByText('Informe o motivo.')).toBeVisible();
    await page.getByLabel(/Motivo/).fill('aprovado por engano');
    await page.getByRole('button', { name: 'Desfazer aprovação', exact: true }).click();
    await expect(page.getByText('Aprovação desfeita: o pedido voltou para pendente.')).toBeVisible();
    await expect(linha(nomes.pago).getByRole('button', { name: 'Aprovar' })).toBeVisible();
  });

  await test.step('reabre o pedido recusado', async () => {
    await linha(nomes.recusado).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: /Reabrir pedido recusado/ }).click();
    await page.getByRole('button', { name: 'Reabrir', exact: true }).click();
    await expect(page.getByText('Pedido reaberto: está pendente.')).toBeVisible();
    await expect(linha(nomes.recusado).getByRole('button', { name: 'Aprovar' })).toBeVisible();
  });

  await test.step('corrige altura e e-mail da ficha', async () => {
    await linha(nomes.pendente).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: 'Corrigir dados da ficha' }).click();
    await page.getByLabel('Altura (cm)').fill('191');
    await page.getByLabel('E-mail').fill('corrigido@teste.com');
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Dados da ficha salvos.')).toBeVisible();
    await expect(linha(nomes.pendente).getByText(/191 cm/)).toBeVisible();
  });

  await test.step('o Administrador corrige a identificação', async () => {
    await linha(nomes.pendente).getByRole('button', { name: /Ações/ }).click();
    await page.getByRole('menuitem', { name: /Corrigir identificação/ }).click();
    await page.getByLabel('Nome completo').fill(`${nomes.pendente} Corrigido`);
    await page.getByRole('button', { name: 'Salvar' }).click();
    await expect(page.getByText('Identificação corrigida e registrada na auditoria.')).toBeVisible();
    await expect(linha(`${nomes.pendente} Corrigido`)).toBeVisible();
  });

  await test.step('exporta o CSV: sem CPF por padrão', async () => {
    await page.getByRole('button', { name: /Exportar lista/ }).click();
    const download = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Baixar CSV' }).click();
    const arquivo = await (await download).path();
    const csv = (await readFile(arquivo!, 'utf8')).replace(/^﻿/, '');
    expect(csv.split('\r\n')[0]).toContain('Nome;Idade;Perfil;Altura (cm)');
    expect(csv).toContain(nomes.pago);
    expect(csv).not.toContain('CPF');
    await expect(page.getByText('Arquivo gerado (a exportação foi registrada).')).toBeVisible();
  });

  await test.step('importa uma planilha: confere antes, exige o consentimento e cria só a linha certa', async () => {
    const novo = `Importado Planilha ${sufixo}`;
    const csv = [
      'nome;cpf;nascimento;sexo;e-mail;telefone;categoria;altura;peso;posição',
      `${novo};${cpfValido(semente + 90)};10/05/1975;M;imp.${sufixo}@teste.com;71999990009;Master 40+;180;82;Ala`,
      `Linha Ruim;123.456.789-00;10/05/1975;M;ruim@teste.com;;Master 40+;;;`,
    ].join('\n');
    await page.getByRole('button', { name: /Importar planilha/ }).click();
    await page.locator('#imp-arq').setInputFiles({ name: 'inscricoes.csv', mimeType: 'text/csv', buffer: Buffer.from(csv, 'utf8') });
    await expect(page.getByText(/2 linhas: 1 serão criadas · 0 ignoradas · 1 com erro/)).toBeVisible();
    await expect(page.getByText('CPF inválido.')).toBeVisible();
    await page.getByRole('button', { name: 'Importar 1 ficha' }).click();
    await expect(page.getByText('Confirme o consentimento LGPD para importar.')).toBeVisible();
    await page.getByLabel(/consentimento LGPD das pessoas/).check();
    await page.getByRole('button', { name: 'Importar 1 ficha' }).click();
    await expect(page.getByText('Importação concluída: 1 criadas, 0 ignoradas, 1 com erro.')).toBeVisible();
    await expect(linha(novo)).toBeVisible();
    await page.screenshot({ path: 'test-results/g7-inscricoes.png', fullPage: true });
  });
});

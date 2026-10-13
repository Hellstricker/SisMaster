import type { Sexo } from './categorias';
import { BASE, enviar, enviarComResposta, lerErro, obter } from './http';

export type StatusInscricao = 'Pendente' | 'AguardandoPagamento' | 'Efetivada' | 'Recusada';
export type PerfilPessoa = 'Convidado' | 'Associado';

export interface PagamentoInscricao {
  id: string;
  tipo: 'Taxa' | 'Saldo';
  valor: number;
  data: string;
  /** Estornado: continua no histórico, mas não conta no total pago. */
  estornado: boolean;
  estornadoEm: string | null;
  motivoEstorno: string | null;
}

/** Conta financeira da ficha (compartilhada por todas as categorias do mesmo envio). */
export interface Financeiro {
  taxaPaga: boolean;
  totalPago: number;
  cobrancaGerada: boolean;
  valorTotal: number | null;
  valorDesconto: number | null;
  valorFinal: number | null;
  saldo: number | null;
  pagamentos: PagamentoInscricao[];
}

/** Pedido de uma categoria dentro da ficha (cada um segue o seu fluxo). */
export interface CategoriaPedida {
  id: string;
  temporadaCategoriaId: string;
  nome: string;
  status: StatusInscricao;
  justificativaExcecao: string | null;
  recusadaEm: string | null;
  motivoRecusa: string | null;
  foraDoEsperado: string[];
}

/** Ficha de um envio: a pessoa, seus dados, a conta financeira e as categorias pedidas. */
export interface Ficha {
  id: string;
  pessoa: {
    id: string;
    nome: string;
    cpf: string;
    nascimento: string;
    idade: number;
    sexo: Sexo;
    perfil: PerfilPessoa;
    email: string;
    telefone: string | null;
  };
  dados: {
    alturaCm: number | null;
    pesoKg: number | null;
    posicao: string | null;
    possuiPlanoSaude: boolean;
    nomePlanoSaude: string | null;
  };
  dataEnvio: string;
  /** Número do pedido feito pelo site (ex.: 2027-0142); nulo nas fichas feitas pela diretoria. */
  protocolo: string | null;
  origem: 'Site' | 'Diretoria';
  /** O cadastro está ligado a uma conta do site. */
  contaLigada: boolean;
  autorizacoes: { dados: string; imagem: string | null; versaoTermos: string | null };
  /** E-mail da conta do site cuja ligação com este cadastro a diretoria autorizou (some depois de usada). */
  ligacaoAutorizadaPara: string | null;
  /** Associado é por temporada: tem pedido efetivado (taxa paga) nesta temporada. */
  associadoNaTemporada: boolean;
  financeiro: Financeiro;
  categorias: CategoriaPedida[];
}

export interface NovaInscricao {
  temporadaId: string;
  nome: string;
  cpf: string;
  nascimento: string; // ISO yyyy-MM-dd
  sexo: Sexo;
  email: string;
  telefone?: string;
  alturaCm?: number;
  pesoKg?: number;
  posicao?: string;
  possuiPlanoSaude: boolean;
  nomePlanoSaude?: string;
  consentimentoLgpd: boolean;
  temporadaCategoriaIds: string[];
}

export function listarFichasDaTemporada(temporadaId: string): Promise<Ficha[]> {
  return obter<Ficha[]>(`/api/temporadas/${temporadaId}/inscricoes`, 'Erro ao carregar inscrições');
}

export function enviarInscricao(dados: NovaInscricao): Promise<void> {
  return enviar('POST', '/api/inscricoes', dados, 'Erro ao enviar inscrição');
}

export function aprovarInscricao(id: string, justificativaExcecao?: string, avisarPorEmail = false): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${id}/aprovar`, { justificativaExcecao, avisarPorEmail }, 'Erro ao aprovar inscrição');
}

export function recusarInscricao(id: string, motivo: string, avisarPorEmail = false): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${id}/recusar`, { motivo, avisarPorEmail }, 'Erro ao recusar inscrição');
}

export function registrarPagamentoTaxa(inscricaoId: string, dataPagamento: string, avisarPorEmail = false): Promise<void> {
  return enviar('POST', `/api/inscricoes/${inscricaoId}/pagamentos/taxa`, { dataPagamento, avisarPorEmail }, 'Erro ao registrar a taxa');
}

export function registrarPagamentoSaldo(inscricaoId: string, valor: number, dataPagamento: string): Promise<void> {
  return enviar('POST', `/api/inscricoes/${inscricaoId}/pagamentos/saldo`, { valor, dataPagamento }, 'Erro ao registrar o pagamento');
}

// ---- G7: correções, exportação e importação

export interface DadosDaFicha {
  alturaCm: number | null;
  pesoKg: number | null;
  posicao: string | null;
  possuiPlanoSaude: boolean;
  nomePlanoSaude: string | null;
  email: string;
  telefone: string | null;
}

export function editarDadosDaFicha(inscricaoId: string, dados: DadosDaFicha): Promise<void> {
  return enviar('PATCH', `/api/inscricoes/${inscricaoId}/dados`, dados, 'Erro ao salvar os dados da ficha');
}

export function estornarPagamento(inscricaoId: string, pagamentoId: string, motivo: string): Promise<void> {
  return enviar('POST', `/api/inscricoes/${inscricaoId}/pagamentos/${pagamentoId}/estornar`, { motivo }, 'Erro ao estornar o pagamento');
}

export function desfazerAprovacao(inscricaoCategoriaId: string, motivo: string): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${inscricaoCategoriaId}/desfazer-aprovacao`, { motivo }, 'Erro ao desfazer a aprovação');
}

export function reabrirPedido(inscricaoCategoriaId: string): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${inscricaoCategoriaId}/reabrir`, undefined, 'Erro ao reabrir o pedido');
}

export interface Identificacao {
  nome: string;
  cpf: string;
  nascimento: string; // ISO yyyy-MM-dd
  sexo: Sexo;
}

/** Só o Administrador: nome, CPF, nascimento e sexo definem a elegibilidade. */
export function corrigirIdentificacao(pessoaId: string, dados: Identificacao): Promise<void> {
  return enviar('PATCH', `/api/pessoas/${pessoaId}/identificacao`, dados, 'Erro ao corrigir a identificação');
}

/** Só o Administrador: autoriza a ligação deste cadastro com a conta do site de outro e-mail (vazio cancela). */
export function autorizarLigacaoDeConta(pessoaId: string, email: string): Promise<void> {
  return enviar('POST', `/api/pessoas/${pessoaId}/autorizar-conta`, { email }, 'Erro ao autorizar a ligação com a conta');
}

/** Só o Administrador: passa as fichas (e a conta do site) do cadastro duplicado, achado pelo CPF, para este cadastro e apaga o duplicado. */
export function unirCadastros(pessoaId: string, cpfDuplicada: string): Promise<void> {
  return enviar('POST', `/api/pessoas/${pessoaId}/unir`, { cpfDuplicada }, 'Erro ao unir os cadastros');
}

export interface FiltroDeExportacao {
  status: StatusInscricao | 'Todas';
  categoriaId: string; // 'todas' ou id da categoria da temporada
  busca: string;
  cpf: boolean;
  contato: boolean;
  ficha: boolean;
}

/** Baixa o CSV (o servidor aplica o filtro, só inclui CPF/contato se pedidos e registra a exportação). */
export async function exportarCsv(temporadaId: string, filtro: FiltroDeExportacao, nomeDoArquivo: string): Promise<void> {
  const q = new URLSearchParams();
  if (filtro.status !== 'Todas') q.set('status', filtro.status);
  if (filtro.categoriaId !== 'todas') q.set('categoriaId', filtro.categoriaId);
  if (filtro.busca.trim()) q.set('busca', filtro.busca.trim());
  q.set('cpf', String(filtro.cpf));
  q.set('contato', String(filtro.contato));
  q.set('ficha', String(filtro.ficha));
  const res = await fetch(`${BASE}/api/temporadas/${temporadaId}/inscricoes/exportar?${q}`);
  if (!res.ok) throw await lerErro(res, 'Não foi possível exportar a lista');
  const url = URL.createObjectURL(await res.blob());
  const a = document.createElement('a');
  a.href = url;
  a.download = nomeDoArquivo;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export interface LinhaDaImportacao {
  linha: number;
  nome: string;
  resultado: 'Criar' | 'Ignorar' | 'Erro';
  mensagens: string[];
  avisos: string[];
}

export interface PreviaDaImportacao {
  total: number;
  aContar: number;
  ignoradas: number;
  comErro: number;
  linhas: LinhaDaImportacao[];
}

export interface ResultadoDaImportacao {
  criadas: number;
  ignoradas: number;
  comErro: number;
  linhas: LinhaDaImportacao[];
}

export function previaDaImportacao(temporadaId: string, csv: string): Promise<PreviaDaImportacao> {
  return enviarComResposta('POST', `/api/temporadas/${temporadaId}/inscricoes/importacao/previa`, { csv }, 'Não foi possível conferir a planilha');
}

export function importarPlanilha(temporadaId: string, csv: string): Promise<ResultadoDaImportacao> {
  return enviarComResposta('POST', `/api/temporadas/${temporadaId}/inscricoes/importacao`, { csv, consentimentoConfirmado: true }, 'Não foi possível importar a planilha');
}

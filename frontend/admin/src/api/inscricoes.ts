import type { Sexo } from './categorias';
import { enviar, obter } from './http';

export type StatusInscricao = 'Pendente' | 'AguardandoPagamento' | 'Efetivada' | 'Recusada';
export type PerfilPessoa = 'Convidado' | 'Associado';

export interface PagamentoInscricao {
  id: string;
  tipo: 'Taxa' | 'Saldo';
  valor: number;
  data: string;
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
  };
  dados: {
    alturaCm: number | null;
    pesoKg: number | null;
    posicao: string | null;
    possuiPlanoSaude: boolean;
    nomePlanoSaude: string | null;
  };
  dataEnvio: string;
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

export function aprovarInscricao(id: string, justificativaExcecao?: string): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${id}/aprovar`, { justificativaExcecao }, 'Erro ao aprovar inscrição');
}

export function recusarInscricao(id: string, motivo: string): Promise<void> {
  return enviar('PATCH', `/api/inscricoes-categorias/${id}/recusar`, { motivo }, 'Erro ao recusar inscrição');
}

export function registrarPagamentoTaxa(inscricaoId: string, dataPagamento: string): Promise<void> {
  return enviar('POST', `/api/inscricoes/${inscricaoId}/pagamentos/taxa`, { dataPagamento }, 'Erro ao registrar a taxa');
}

export function registrarPagamentoSaldo(inscricaoId: string, valor: number, dataPagamento: string): Promise<void> {
  return enviar('POST', `/api/inscricoes/${inscricaoId}/pagamentos/saldo`, { valor, dataPagamento }, 'Erro ao registrar o pagamento');
}

import { enviar, enviarComResposta, obter } from './http';

export type SituacaoEquipeInscrita = 'AguardandoConfirmacoes' | 'EmAnalise' | 'Aprovada' | 'Recusada';
export type StatusConvite = 'Enviado' | 'Confirmado' | 'Recusou' | 'Vencido';

export interface EquipeInscrita {
  id: string;
  protocolo: string;
  nome: string;
  categoria: string | null;
  responsavel: string;
  responsavelEmail: string;
  responsavelTelefone: string | null;
  situacao: SituacaoEquipeInscrita;
  enviadaEm: string;
  confirmados: number;
  emAberto: number;
  equipeId: string | null;
}

export interface EquipesInscritasDaTemporada {
  temporada: { id: string; campeonato: string | null; ano: number; taxaInscricao: number | null; aberta: boolean };
  equipes: EquipeInscrita[];
}

export interface AtletaInscrito {
  id: string;
  nome: string;
  email: string;
  ehResponsavel: boolean;
  cpf: string | null;
  nascimento: string | null;
  status: StatusConvite;
  respondidoEm: string | null;
  fichaId: string | null;
  pedidoId: string | null;
  pedido: 'Pendente' | 'AguardandoPagamento' | 'Efetivada' | 'Recusada' | null;
  taxaPaga: boolean;
  naEquipe: boolean;
}

export interface DetalheEquipeInscrita {
  id: string;
  protocolo: string;
  nome: string;
  categoria: string | null;
  taxa: number | null;
  situacao: SituacaoEquipeInscrita;
  motivoRecusa: string | null;
  justificativaExcecao: string | null;
  responsavel: { nome: string; email: string; telefone: string | null };
  minimo: number;
  maximo: number;
  confirmados: number;
  emAberto: number;
  atletas: AtletaInscrito[];
}

export interface ResultadoDaTaxa { inscricaoId: string; ok: boolean; erro: string | null }

export const listarEquipesInscritas = (temporadaId: string) =>
  obter<EquipesInscritasDaTemporada>(`/api/temporadas/${temporadaId}/equipes-inscritas`, 'Erro ao carregar as equipes inscritas');

export const obterEquipeInscrita = (id: string) =>
  obter<DetalheEquipeInscrita>(`/api/equipes-inscritas/${id}`, 'Erro ao carregar a equipe');

export const aprovarEquipeInscrita = (id: string, justificativaExcecao?: string) =>
  enviar('POST', `/api/equipes-inscritas/${id}/aprovar`, { justificativaExcecao: justificativaExcecao || null }, 'Erro ao aprovar a equipe');

export const recusarEquipeInscrita = (id: string, motivo: string) =>
  enviar('POST', `/api/equipes-inscritas/${id}/recusar`, { motivo }, 'Erro ao recusar a equipe');

/** Registra a taxa de vários atletas (a data é ISO AAAA-MM-DD). Os que falham voltam com o motivo, sem desfazer os demais. */
export const registrarTaxasDaEquipe = (id: string, dataPagamento: string, inscricaoIds: string[]) =>
  enviarComResposta<{ resultados: ResultadoDaTaxa[] }>('POST', `/api/equipes-inscritas/${id}/taxas`, { dataPagamento, inscricaoIds }, 'Erro ao registrar as taxas');

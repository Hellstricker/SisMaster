import type { Sexo } from './categorias';
import { enviar, obter } from './http';

export type TipoDesconto = 'Percentual' | 'Valor';

export interface TemporadaCategoria {
  id: string;
  categoriaId: string;
  nome: string;
  idadeMinima: number;
  sexo: Sexo | null;
  aceitaAbaixoIdadeMinima: boolean;
  minimoPeriodosEmQuadra: number;
  minimoPeriodosForaQuadra: number;
  valor: number | null;
}

/** Desconto de quem joga exatamente esta combinação de categorias da temporada. */
export interface Desconto {
  id: string;
  temporadaCategoriaIds: string[];
  /** Nomes das categorias da combinação (calculado pela API). */
  categorias: string[];
  tipo: TipoDesconto;
  valor: number;
}

export interface DadosDesconto {
  temporadaCategoriaIds: string[];
  tipo: TipoDesconto;
  valor: number;
}

export interface TemporadaDetalhe {
  id: string;
  campeonatoId: string;
  associacaoId: string;
  ano: number;
  status: string;
  dataInicioInscricoes: string;
  dataFimInscricoes: string;
  inscricoesHabilitadas: boolean;
  valorBonificacaoPorAtleta: number;
  taxaInscricao: number | null;
  descontos: Desconto[];
  categorias: TemporadaCategoria[];
}

export function obterTemporada(id: string): Promise<TemporadaDetalhe> {
  return obter<TemporadaDetalhe>(`/api/temporadas/${id}`, 'Erro ao carregar temporada');
}

export function adicionarCategoria(temporadaId: string, data: { categoriaId: string }): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/categorias`, data, 'Erro ao adicionar categoria');
}

export function removerCategoria(temporadaId: string, categoriaId: string): Promise<void> {
  return enviar('DELETE', `/api/temporadas/${temporadaId}/categorias/${categoriaId}`, undefined, 'Erro ao remover categoria');
}

export function definirValorCategoria(temporadaId: string, categoriaId: string, valor: number | null): Promise<void> {
  return enviar('PATCH', `/api/temporadas/${temporadaId}/categorias/${categoriaId}/valor`, { valor }, 'Erro ao definir valor');
}

export function definirTaxaInscricao(temporadaId: string, taxa: number | null): Promise<void> {
  return enviar('PATCH', `/api/temporadas/${temporadaId}/taxa-inscricao`, { taxa }, 'Erro ao definir a taxa');
}

export function definirDesconto(temporadaId: string, desconto: DadosDesconto): Promise<void> {
  return enviar('PUT', `/api/temporadas/${temporadaId}/descontos`, desconto, 'Erro ao definir desconto');
}

export function removerDesconto(temporadaId: string, descontoId: string): Promise<void> {
  return enviar('DELETE', `/api/temporadas/${temporadaId}/descontos/${descontoId}`, undefined, 'Erro ao remover desconto');
}

export function gerarCobrancas(temporadaId: string): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/gerar-cobrancas`, undefined, 'Erro ao gerar cobranças');
}

export function alterarInscricoesHabilitadas(campeonatoId: string, temporadaId: string, habilitadas: boolean): Promise<void> {
  return enviar(
    'PATCH',
    `/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/inscricoes-habilitadas`,
    { habilitadas },
    'Erro ao alterar as inscrições',
  );
}

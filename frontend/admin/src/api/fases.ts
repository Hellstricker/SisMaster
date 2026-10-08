import { enviar, obter } from './http';

export type TipoFase = 'PontosCorridos' | 'Grupos' | 'MataMata';
export type StatusFase = 'Planejada' | 'EmAndamento' | 'Encerrada';
export type DistribuicaoEquipes = 'Serpentina' | 'Alternada' | 'Manual';

export interface FaseItem {
  id: string;
  ordem: number;
  nome: string;
  tipo: TipoFase;
  /** Derivado dos jogos da fase (não é editável). */
  status: StatusFase;
  numeroTurnos: number | null;
  numeroGrupos: number | null;
  distribuicao: DistribuicaoEquipes | null;
  jogosPorConfronto: number | null;
  numeroConfrontos: number | null;
  /** Quantos se classificam (por grupo, em Grupos). Nulo = todos. */
  classificadosPrimeiros: number | null;
  /** Só em Grupos: melhores (N+1)º colocados que também se classificam. */
  melhoresExtras: number;
  /** Fases cadastradas antes dos campos de estrutura podem estar incompletas. */
  estruturaCompleta: boolean;
  faseAnteriorId: string | null;
  faseAnteriorNome: string | null;
  /** Outra fase vem desta: não pode ser excluída. */
  temDependente: boolean;
}

export interface CategoriaFases {
  temporadaCategoriaId: string;
  nome: string;
  equipes: number;
  fases: FaseItem[];
}

export interface FasesDaTemporada {
  id: string;
  ano: number;
  status: string;
  permiteCadastrar: boolean;
  cadastroEncerrado: boolean;
  cadastroFasesEncerradoEm: string | null;
  tabelaJogosGerada: boolean;
  podeReabrir: boolean;
  categorias: CategoriaFases[];
}

export interface DadosFase {
  nome: string;
  tipo: TipoFase;
  numeroTurnos: number | null;
  numeroGrupos: number | null;
  distribuicao: DistribuicaoEquipes | null;
  jogosPorConfronto: number | null;
  numeroConfrontos: number | null;
  classificadosPrimeiros: number | null;
  melhoresExtras: number;
  faseAnteriorId: string | null;
}

/**
 * Prévia da distribuição de N equipes (posições 1..N) em G grupos. Serpentina: 1-A, 2-B, 3-B, 4-A… (8 equipes,
 * 2 grupos: A = 1,4,5,8 e B = 2,3,6,7). Alternada: 1-A, 2-B, 3-A, 4-B… (A = 1,3,5,7 e B = 2,4,6,8).
 */
export function distribuirEmGrupos(equipes: number, grupos: number, modo: 'Serpentina' | 'Alternada'): number[][] {
  const resultado: number[][] = Array.from({ length: grupos }, () => []);
  for (let i = 0; i < equipes; i++) {
    const noCiclo = i % grupos;
    const indo = modo === 'Alternada' || Math.floor(i / grupos) % 2 === 0;
    resultado[indo ? noCiclo : grupos - 1 - noCiclo].push(i + 1);
  }
  return resultado;
}

export function obterFasesDaTemporada(temporadaId: string): Promise<FasesDaTemporada> {
  return obter<FasesDaTemporada>(`/api/temporadas/${temporadaId}/fases`, 'Erro ao carregar as fases');
}

export function criarFase(temporadaId: string, temporadaCategoriaId: string, dados: DadosFase): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/fases`, { temporadaCategoriaId, ...dados }, 'Erro ao criar fase');
}

export function editarFase(faseId: string, dados: DadosFase): Promise<void> {
  return enviar('PATCH', `/api/fases/${faseId}`, dados, 'Erro ao editar fase');
}

export function excluirFase(faseId: string): Promise<void> {
  return enviar('DELETE', `/api/fases/${faseId}`, undefined, 'Erro ao excluir fase');
}

/** direcao: -1 sobe uma posição na sequência; +1 desce. */
export function moverFase(faseId: string, direcao: -1 | 1): Promise<void> {
  return enviar('PATCH', `/api/fases/${faseId}/mover`, { direcao }, 'Erro ao mover fase');
}

export function encerrarCadastroFases(temporadaId: string): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/fases/encerrar-cadastro`, undefined, 'Erro ao encerrar o cadastro de fases');
}

export function reabrirCadastroFases(temporadaId: string): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/fases/reabrir-cadastro`, undefined, 'Erro ao reabrir o cadastro de fases');
}

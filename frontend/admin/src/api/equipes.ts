import type { PerfilPessoa } from './inscricoes';
import { enviar, obter } from './http';

export interface AtletaItem {
  /** Id do vínculo com a equipe; vazio (Guid.Empty) quando ainda não está em equipe. */
  id: string;
  inscricaoCategoriaId: string;
  nome: string;
  perfil: PerfilPessoa;
  /** Saldo em aberto da cobrança final (aviso, não bloqueia). */
  saldoEmAberto: number | null;
}

export interface EquipeItem {
  id: string;
  nome: string;
  cor: string | null;
  atletas: AtletaItem[];
}

export interface CategoriaEquipes {
  temporadaCategoriaId: string;
  nome: string;
  equipes: EquipeItem[];
  semEquipe: AtletaItem[];
}

export interface EquipesDaTemporada {
  id: string;
  ano: number;
  status: string;
  permiteFormarEquipes: boolean;
  categorias: CategoriaEquipes[];
}

export function obterEquipesDaTemporada(temporadaId: string): Promise<EquipesDaTemporada> {
  return obter<EquipesDaTemporada>(`/api/temporadas/${temporadaId}/equipes`, 'Erro ao carregar as equipes');
}

export function criarEquipe(temporadaId: string, dados: { temporadaCategoriaId: string; nome: string; cor: string }): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/equipes`, dados, 'Erro ao criar equipe');
}

export function editarEquipe(equipeId: string, dados: { nome: string; cor: string }): Promise<void> {
  return enviar('PATCH', `/api/equipes/${equipeId}`, dados, 'Erro ao editar equipe');
}

export function excluirEquipe(equipeId: string): Promise<void> {
  return enviar('DELETE', `/api/equipes/${equipeId}`, undefined, 'Erro ao excluir equipe');
}

export function adicionarAtleta(equipeId: string, inscricaoCategoriaId: string): Promise<void> {
  return enviar('POST', `/api/equipes/${equipeId}/atletas`, { inscricaoCategoriaId }, 'Erro ao adicionar atleta');
}

export function removerAtleta(equipeId: string, atletaId: string): Promise<void> {
  return enviar('DELETE', `/api/equipes/${equipeId}/atletas/${atletaId}`, undefined, 'Erro ao remover atleta');
}

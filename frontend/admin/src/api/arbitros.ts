import { enviar, obter } from './http';
import type { LocalItem, StatusJogo } from './jogos';

export type StatusSumula = 'EmPreparacao' | 'EmAndamento' | 'Intervalo' | 'Encerrada';

/** Jogo já agendado e ainda não terminado, como o árbitro o vê. */
export interface JogoDoArbitro {
  id: string;
  numero: number;
  temporada: { id: string; ano: number };
  categoria: { id: string; nome: string };
  fase: { id: string; nome: string };
  /** "R3" em pontos corridos/grupos; "Final · J2" no mata-mata. */
  etiqueta: string;
  casa: { texto: string; definida: boolean };
  visitante: { texto: string; definida: boolean };
  /** As duas equipes já estão definidas: a súmula pode ser preparada. */
  podePrepararSumula: boolean;
  data: string;
  hora: string | null;
  local: LocalItem | null;
  status: StatusJogo;
  sumula: { id: string; status: StatusSumula } | null;
}

export function listarJogosDoArbitro(): Promise<{ jogos: JogoDoArbitro[] }> {
  return obter<{ jogos: JogoDoArbitro[] }>('/api/arbitros/jogos', 'Erro ao carregar os jogos');
}

/** Encerra a súmula: o placar vira o resultado do jogo, que passa a Encerrado. */
export function encerrarSumula(sumulaId: string): Promise<void> {
  return enviar('POST', `/api/sumulas/${sumulaId}/encerrar`, undefined, 'Erro ao encerrar a súmula');
}

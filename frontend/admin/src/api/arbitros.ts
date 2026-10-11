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
  sumula: { id: string; status: StatusSumula; importada: boolean } | null;
}

export function listarJogosDoArbitro(): Promise<{ jogos: JogoDoArbitro[] }> {
  return obter<{ jogos: JogoDoArbitro[] }>('/api/arbitros/jogos', 'Erro ao carregar os jogos');
}

/** O que a tela de encerramento mostra antes de confirmar. */
export interface SituacaoEncerramento {
  status: StatusSumula;
  periodo: number;
  placarCasa: number;
  placarVisitante: number;
  casa: string;
  visitante: string;
  segundosRestantes: number;
  /** No basquete não há empate: bloqueia o encerramento. */
  empatado: boolean;
  /** Antes do 4º período ou com tempo no relógio: só encerra com a confirmação. */
  antesDoTempo: boolean;
  cronometroAtivo: boolean;
}

export function situacaoDoEncerramento(sumulaId: string): Promise<SituacaoEncerramento> {
  return obter<SituacaoEncerramento>(`/api/sumulas/${sumulaId}/encerramento`, 'Erro ao carregar a situação do jogo');
}

/** Encerra a súmula: o placar vira o resultado do jogo, que passa a Encerrado. `confirmarAntesDoTempo` cobre o jogo que terminou antes do fim. */
export function encerrarSumula(sumulaId: string, confirmarAntesDoTempo = false): Promise<void> {
  return enviar('POST', `/api/sumulas/${sumulaId}/encerrar`, { confirmarAntesDoTempo }, 'Erro ao encerrar a súmula');
}

/** Reabre a súmula encerrada para correção (árbitro ou gestão). O motivo é obrigatório e fica no histórico do jogo. */
export function reabrirSumula(sumulaId: string, motivo: string): Promise<void> {
  return enviar('POST', `/api/sumulas/${sumulaId}/reabrir`, { motivo }, 'Erro ao reabrir a súmula');
}

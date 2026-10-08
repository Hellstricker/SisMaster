import { ErroDaApi, BASE, enviarComResposta, lerMensagens } from './http';
import type { LadoTime } from './jogos';

export interface PreviaJogadorFiba {
  camisa: string;
  nomeFeed: string;
  /** Nulo = a camisa não está na relação da súmula. */
  nomeSumula: string | null;
  titularFeed: boolean;
  titularSumula: boolean | null;
  jogou: boolean;
  divergencias: string[];
}

export interface PreviaPeriodoFiba {
  periodo: number;
  placarFeed: number | null;
  placarReconstruido: number;
}

export interface PreviaTimeFiba {
  lado: LadoTime;
  nomeFeed: string;
  nomeSumula: string;
  placarFeed: number;
  placarReconstruido: number;
  periodos: PreviaPeriodoFiba[];
  jogadores: PreviaJogadorFiba[];
}

/** Lance da FIBA atribuído à equipe, não a um jogador: não entra na súmula. `tempoRestanteSegundos` é o que faltava no período. */
export interface EventoDeEquipeFiba {
  lado: LadoTime;
  tipo: string;
  periodo: number | string;
  tempoRestanteSegundos: number;
}

export interface PreviaImportacaoFiba {
  times: PreviaTimeFiba[];
  eventos: number;
  trocas: number;
  eventosDeEquipe: EventoDeEquipeFiba[];
  naoMapeados: Record<string, number>;
  problemas: string[];
  avisos: string[];
  podeAplicar: boolean;
}

/** Baixa o jogo do FIBA LiveStats e o confere com a súmula; nada é gravado. */
export function previaImportacaoFiba(sumulaId: string, codigo: string, inverterLados: boolean): Promise<PreviaImportacaoFiba> {
  return enviarComResposta('POST', `/api/sumulas/${sumulaId}/importacao-fiba/previa`, { codigo, inverterLados }, 'Erro ao conferir o jogo no LiveStats');
}

/** Substitui os dados da súmula pelos do jogo e encerra o jogo. Se a conferência não fechar, lança com todos os problemas. */
export async function importarFiba(sumulaId: string, codigo: string, inverterLados: boolean): Promise<void> {
  const res = await fetch(`${BASE}/api/sumulas/${sumulaId}/importacao-fiba`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ codigo, inverterLados }),
  });
  if (!res.ok) {
    const mensagens = await lerMensagens(res);
    throw new ErroDaApi(mensagens.length ? mensagens : ['Erro ao importar o jogo']);
  }
}

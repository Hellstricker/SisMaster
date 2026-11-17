/** Tipos e regras de tela da inscrição de equipe em torneio aberto (o servidor revalida tudo). */

export const MINIMO_DE_ATLETAS = 5;
export const ATLETAS_PARA_RODIZIO = 7;
export const MAXIMO_DE_ATLETAS = 15;

export type SituacaoDaEquipe = 'AguardandoConfirmacoes' | 'EmAnalise' | 'Aprovada' | 'Recusada';
export type StatusDoConvite = 'Enviado' | 'Confirmado' | 'Recusou' | 'Vencido';

export const ROTULO_EQUIPE: Record<SituacaoDaEquipe, string> = {
  AguardandoConfirmacoes: 'Aguardando confirmações', EmAnalise: 'Em análise', Aprovada: 'Equipe aprovada', Recusada: 'Recusada',
};

/** Classe de cor do selo (as mesmas dos pedidos de inscrição). */
export const CLASSE_EQUIPE: Record<SituacaoDaEquipe, string> = {
  AguardandoConfirmacoes: 'st-Pendente', EmAnalise: 'st-Pendente', Aprovada: 'st-Efetivada', Recusada: 'st-Recusada',
};

export const ROTULO_CONVITE: Record<StatusDoConvite, string> = {
  Enviado: 'Convite enviado', Confirmado: 'Confirmou', Recusou: 'Recusou o convite', Vencido: 'Convite vencido',
};

export interface MinhaEquipeItem {
  id: string;
  protocolo: string;
  nome: string;
  campeonato: string | null;
  ano: number | null;
  temporadaId: string;
  categoria: string | null;
  situacao: SituacaoDaEquipe;
  enviadaEm: string;
  confirmados: number;
  convidados: number;
}

export interface AtletaDaEquipe {
  id: string;
  nome: string;
  email: string;
  ehResponsavel: boolean;
  status: StatusDoConvite;
  validoAte: string | null;
  respondidoEm: string | null;
  /** Situação do pedido do atleta (depois de confirmar). */
  pedido: string | null;
  taxaPaga: boolean;
  naEquipe: boolean;
}

export interface DetalheDaEquipe {
  id: string;
  protocolo: string;
  nome: string;
  campeonato: string | null;
  ano: number | null;
  categoria: string | null;
  taxa: number | null;
  situacao: SituacaoDaEquipe;
  motivoRecusa: string | null;
  enviadaEm: string;
  decididaEm: string | null;
  minimo: number;
  maximo: number;
  confirmados: number;
  emAberto: number;
  responsavel: { nome: string; email: string; telefone: string | null };
  atletas: AtletaDaEquipe[];
}

export interface ConviteConsultado {
  equipe: string;
  responsavel: string;
  campeonato: string | null;
  ano: number | null;
  categoria: string | null;
  taxa: number | null;
  associacaoSigla: string | null;
  temporadaId: string;
  primeiroNome: string;
  emailMascarado: string;
  situacao: 'Enviado' | 'Confirmado' | 'Recusou' | 'Removido' | 'Vencido' | 'EquipeRecusada';
  validoAte: string;
}

export interface AtletaDoElenco {
  nome: string;
  cpf: string;
  /** DD/MM/AAAA */
  nascimento: string;
  sexo: 'Masculino' | 'Feminino';
  email: string;
}

import type { SituacaoDaFicha, StatusDoPedido } from './inscricao';
import { isoParaBr } from './inscricao';

/** O detalhe de uma inscrição da conta (I7), como a API devolve. */
export interface DetalheDaInscricao {
  id: string;
  protocolo: string | null;
  origem: string;
  enviadaEm: string;
  campeonato: string | null;
  ano: number | null;
  temporadaId: string;
  situacao: SituacaoDaFicha;
  categorias: { id: string; nome: string; status: StatusDoPedido; motivoRecusa: string | null; recusadaEm: string | null; equipe: string | null }[];
  financeiro: {
    taxa: number | null; pago: number; taxaPaga: boolean; algumPedidoAprovado: boolean;
    cobrancaGerada: boolean; valorTotal: number | null; desconto: number | null; valorFinal: number | null; saldo: number | null;
  } | null;
  ficha: {
    alturaCm: number | null; pesoKg: number | null; posicao: string | null; possuiPlanoSaude: boolean; nomePlanoSaude: string | null;
    autorizouDados: string; autorizouImagem: string | null; versaoTermos: string | null;
  };
}

export const brl = (v: number) => v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

/** "2026-10-08T19:42:10" -> "08/10/2026 19:42". */
export const dataEHora = (iso: string) => `${isoParaBr(iso.slice(0, 10))} ${iso.slice(11, 16)}`;

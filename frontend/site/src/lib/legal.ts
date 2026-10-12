import type { PaginaDaAssociacao } from './api';

/** Versão e data de vigência dos textos legais. Quando o texto mudar, suba a versão e a data (e, se a mudança for relevante, avise os usuários). */
export const VERSAO_LEGAL = '1.0';
export const VIGENTE_DESDE = '12/10/2026';

const A_INFORMAR = 'a informar pela associação';

/** Os dados da controladora que o texto cita, vindos do cadastro da associação (gestão > Página pública); o que falta aparece como "a informar". */
export function dadosLegais(p: PaginaDaAssociacao | null) {
  return {
    nome: p?.associacao.nome ?? 'a Associação',
    sigla: p?.associacao.sigla ?? 'a Associação',
    cnpj: p?.legal?.cnpj ?? A_INFORMAR,
    endereco: p?.legal?.endereco ?? A_INFORMAR,
    encarregado: p?.legal?.encarregadoDeDados ?? A_INFORMAR,
    emailPrivacidade: p?.legal?.emailPrivacidade ?? p?.contato?.email ?? A_INFORMAR,
    foro: p?.legal?.foroCidade ?? null,
    emailContato: p?.contato?.email ?? p?.legal?.emailPrivacidade ?? A_INFORMAR,
  };
}

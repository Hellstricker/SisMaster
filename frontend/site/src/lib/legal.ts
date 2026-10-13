import type { PaginaDaAssociacao } from './api';

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

export type DadosLegais = ReturnType<typeof dadosLegais>;

/** Troca {{nome}}, {{sigla}}, {{cnpj}}, {{endereco}}, {{encarregado}}, {{emailPrivacidade}}, {{foro}} e {{emailContato}} pelos dados da associação. */
export function preencherTexto(texto: string, d: DadosLegais): string {
  const valores: Record<string, string> = { ...d, foro: d.foro ?? A_INFORMAR };
  return texto.replace(/\{\{\s*(\w+)\s*\}\}/g, (inteiro, chave: string) => valores[chave] ?? inteiro);
}

/** "2026-10-12" -> "12/10/2026". */
export const dataLegal = (iso: string) => iso.slice(0, 10).split('-').reverse().join('/');

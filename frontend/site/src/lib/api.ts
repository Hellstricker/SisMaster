/**
 * Acesso à API pública (somente leitura) pelo servidor do site. O endereço interno e a associação do site vêm de
 * variáveis de ambiente (uma associação fixa por instalação; várias associações são da Fase 3).
 */
const API = process.env.API_INTERNO ?? 'http://localhost:5200';
export const SIGLA = process.env.SITE_ASSOCIACAO_SIGLA ?? 'ABABAS';

export interface Associacao {
  id: string;
  nome: string;
  sigla: string;
  uf: string;
}

export interface CategoriaAberta {
  id: string;
  nome: string;
  idadeMinima: number;
  sexo: string | null;
}

export interface TemporadaAberta {
  temporadaId: string;
  campeonato: string;
  ano: number;
  inicioInscricoes: string;
  fimInscricoes: string;
  diasRestantes: number;
  categorias: CategoriaAberta[];
}

async function obter<T>(caminho: string, segundos = 30): Promise<T | null> {
  try {
    const r = await fetch(`${API}${caminho}`, { next: { revalidate: segundos } });
    if (!r.ok) return null;
    return (await r.json()) as T;
  } catch {
    return null; // API fora do ar: a página mostra o aviso em vez de quebrar
  }
}

export const obterAssociacao = () => obter<Associacao>(`/api/publico/associacoes/${encodeURIComponent(SIGLA)}`, 60);

export async function obterInscricoesAbertas(): Promise<TemporadaAberta[] | null> {
  const r = await obter<{ temporadas: TemporadaAberta[] }>(`/api/publico/associacoes/${encodeURIComponent(SIGLA)}/inscricoes-abertas`);
  return r ? r.temporadas : null;
}

/** "2026-11-01T00:00:00" -> "01/11/2026" (sem passar por Date: não depende de fuso). */
export function dataBr(iso: string): string {
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

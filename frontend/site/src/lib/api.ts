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

// ---------- vitrine ----------

export interface LadoDoJogo { texto: string; definida: boolean }
export interface AoVivo { sumulaId: string; periodo: number; placarCasa: number; placarVisitante: number; segundosRestantes: number; cronometroAtivo: boolean }
export interface CartaoDeJogo {
  id: string;
  numero: number;
  data: string | null;
  hora: string | null;
  local: { nome: string; cidade: string; estado: string | null } | null;
  categoria: string;
  fase: string;
  etiqueta: string;
  casa: LadoDoJogo;
  visitante: LadoDoJogo;
  status: 'Agendado' | 'EmAndamento' | 'Encerrado' | 'WO' | 'Dispensado';
  placarCasa: number | null;
  placarVisitante: number | null;
  temporada: { id: string; campeonato: string; ano: number };
  aoVivo: AoVivo | null;
}

export interface Inicio {
  hoje: string;
  aoVivo: CartaoDeJogo[];
  jogosDeHoje: CartaoDeJogo[];
  proximos: CartaoDeJogo[];
  resultados: CartaoDeJogo[];
}

export interface TemporadaItem { id: string; campeonato: string; ano: number; status: string }

export interface TemporadaPublica {
  id: string;
  campeonato: string;
  ano: number;
  status: string;
  tabelaGerada: boolean;
  totalEquipes: number;
  categorias: { id: string; nome: string }[];
  fases: { id: string; nome: string; ordem: number; tipo: string; categoriaId: string }[];
  equipes: { id: string; nome: string; cor: string | null; categoriaId: string; categoria: string; atletas: number }[];
  jogos: CartaoDeJogo[];
}

export interface LinhaClassificacao {
  posicao: number;
  texto: string;
  equipe: { id: string; nome: string } | null;
  jogos: number; vitorias: number; derrotas: number; derrotasWO: number;
  pontosFeitos: number; pontosSofridos: number; saldo: number; total: number;
  sequencia: string;
  classificado: string | null;
}
export interface LadoDaSerie { texto: string; vitorias: number; venceu: boolean }
export interface Confronto {
  id: string; numero: number; nome: string; decidido: boolean; iniciado: boolean; a: LadoDaSerie; b: LadoDaSerie;
  jogos: { id: string; numero: number; jogoDaSerie: number | null; opcional: boolean; status: string; placarCasa: number | null; placarVisitante: number | null }[];
}
export interface ClassificacaoPublica {
  id: string;
  nome: string;
  tipo: string;
  classificacao:
    | { tipo: 'Tabela'; definitiva: boolean; linhas: LinhaClassificacao[] }
    | { tipo: 'Grupos'; definitiva: boolean; grupos: { nome: string; ordem: number; linhas: LinhaClassificacao[] }[]; melhores: { posicao: number; grupo: string; linha: LinhaClassificacao }[] }
    | { tipo: 'MataMata'; vitoriasNecessarias: number; confrontos: Confronto[] }
    | null;
}

export interface JogadorDaSumula { numero: string; nome: string; pontos: number; rebotes: number; assistencias: number; roubadas: number; tocos: number; faltas: number }
export interface LadoDaSumula { nome: string; tecnico: string | null; jogadores: JogadorDaSumula[] }
export interface JogoPublico {
  jogo: CartaoDeJogo;
  campeonato: { id: string; nome: string; ano: number };
  sumula: {
    status: string; periodo: number; placarCasa: number; placarVisitante: number;
    parciais: { periodo: number; casa: number; visitante: number }[];
    casa: LadoDaSumula; visitante: LadoDaSumula;
  } | null;
}

async function obter<T>(caminho: string, segundos = 30): Promise<T | null> {
  try {
    const r = await fetch(`${API}${caminho}`, segundos === 0 ? { cache: 'no-store' } : { next: { revalidate: segundos } });
    if (!r.ok) return null;
    return (await r.json()) as T;
  } catch {
    return null; // API fora do ar: a página mostra o aviso em vez de quebrar
  }
}

const assoc = `/api/publico/associacoes/${encodeURIComponent(SIGLA)}`;

export const obterAssociacao = () => obter<Associacao>(assoc, 60);

export async function obterInscricoesAbertas(): Promise<TemporadaAberta[] | null> {
  const r = await obter<{ temporadas: TemporadaAberta[] }>(`${assoc}/inscricoes-abertas`, 0);
  return r ? r.temporadas : null;
}

export const obterInicio = () => obter<Inicio>(`${assoc}/inicio`, 0);
export async function obterCampeonatos(): Promise<TemporadaItem[] | null> {
  const r = await obter<{ temporadas: TemporadaItem[] }>(`${assoc}/campeonatos`, 0);
  return r ? r.temporadas : null;
}
export const obterTemporada = (id: string) => obter<TemporadaPublica>(`/api/publico/temporadas/${encodeURIComponent(id)}`, 0);
export const obterClassificacao = (faseId: string) => obter<ClassificacaoPublica>(`/api/publico/fases/${encodeURIComponent(faseId)}/classificacao`, 0);
export const obterJogo = (id: string) => obter<JogoPublico>(`/api/publico/jogos/${encodeURIComponent(id)}`, 0);

// ---------- formatação ----------

/** "2026-11-01T00:00:00" -> "01/11/2026" (sem passar por Date: não depende de fuso). */
export function dataBr(iso: string): string {
  const [a, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

/** "2026-10-12" -> "12/10". */
export function diaMes(iso: string): string {
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}

/** "2026-10-12" -> "seg" (fuso fixo em UTC, ao meio-dia, para não virar o dia). */
export function diaDaSemana(iso: string): string {
  return new Date(`${iso.slice(0, 10)}T12:00:00Z`).toLocaleDateString('pt-BR', { weekday: 'short', timeZone: 'UTC' }).replace('.', '');
}

export function relogio(segundos: number): string {
  return `${Math.floor(segundos / 60)}:${String(segundos % 60).padStart(2, '0')}`;
}

export function rotuloPeriodo(p: number): string {
  return p >= 5 ? 'Prorrogação' : `${p}º período`;
}

// ---------- A associação e contato ----------

export interface PaginaDaAssociacao {
  associacao: { nome: string; sigla: string; uf: string };
  missao: string | null;
  quemSomos: string | null;
  gestao: string | null;
  diretoria: { cargo: string; nome: string }[];
  contato: { whatsapp: string | null; instagram: string | null; email: string | null; ondeJogamos: string | null };
  /** Dados da controladora para a política de privacidade e os termos de uso. */
  legal: { cnpj: string | null; endereco: string | null; encarregadoDeDados: string | null; emailPrivacidade: string | null; foroCidade: string | null };
}

export const obterPagina = () => obter<PaginaDaAssociacao>(`${assoc}/pagina`, 0);

/** WhatsApp digitado como "(71) 9 0000-0000" -> link wa.me com o DDI do Brasil. */
export function linkWhatsapp(texto: string): string | null {
  const d = texto.replace(/\D/g, '');
  if (d.length < 10) return null;
  return `https://wa.me/${d.startsWith('55') && d.length >= 12 ? d : `55${d}`}`;
}

/** "@usuario" ou "instagram.com/usuario" -> link do perfil (só letras, números, ponto e sublinhado). */
export function linkInstagram(texto: string): string | null {
  const m = texto.trim().replace(/\/+$/, '').match(/([A-Za-z0-9._]{1,30})$/);
  return m ? `https://www.instagram.com/${m[1]}/` : null;
}

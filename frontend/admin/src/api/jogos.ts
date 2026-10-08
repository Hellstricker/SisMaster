import { enviar, obter } from './http';
import type { DistribuicaoEquipes, StatusFase, TipoFase } from './fases';

export type TipoReferencia = 'Equipe' | 'Colocacao' | 'Vencedor' | 'Perdedor';
export type StatusJogo = 'Agendado' | 'EmAndamento' | 'Encerrado' | 'WO' | 'Dispensado';

export interface EquipeResumo {
  id: string;
  nome: string;
  cor: string | null;
}

/** De onde vem uma equipe: só os campos do tipo valem. */
export interface Origem {
  tipo: TipoReferencia;
  equipeId: string | null;
  faseId: string | null;
  grupoOrdem: number | null;
  posicao: number | null;
  confrontoId: string | null;
  /** Texto legível calculado pela API (somente leitura). */
  texto?: string;
}

export interface Lado {
  texto: string;
  equipe: EquipeResumo | null;
  posicao: number;
}

export interface LocalItem {
  id: string;
  nome: string;
  cidade: string;
  estado: string | null;
  /** Quantos jogos usam o local (vem da listagem da associação). */
  jogos?: number;
}

export interface JogoDaFase {
  /** Nulo na prévia (a tabela ainda não foi gerada). */
  id: string | null;
  numero: number | null;
  rodada: number;
  jogoDaSerie: number | null;
  opcional: boolean;
  grupo: string | null;
  confrontoId: string | null;
  casa: Lado;
  visitante: Lado;
  /** yyyy-MM-dd */
  data: string | null;
  /** HH:mm:ss */
  hora: string | null;
  local: LocalItem | null;
  status: StatusJogo;
  placarCasa: number | null;
  placarVisitante: number | null;
}

export interface VagaDaFase {
  id: string;
  posicao: number;
  grupoOrdem: number | null;
  origem: Origem;
  equipe: EquipeResumo | null;
  texto: string;
}

export interface GrupoDaFase {
  ordem: number;
  nome: string;
  equipes: VagaDaFase[];
}

export interface ConfrontoDaFase {
  id: string;
  numero: number;
  nome: string;
  origemA: Origem;
  origemB: Origem;
  jogos: JogoDaFase[];
}

export interface FaseAnteriorOpcao {
  id: string;
  nome: string;
  ordem: number;
  tipo: TipoFase;
  numeroGrupos: number | null;
  confrontos: { id: string; numero: number; nome: string }[];
}

export interface DetalheFase {
  id: string;
  nome: string;
  ordem: number;
  tipo: TipoFase;
  status: StatusFase;
  associacaoId: string;
  categoria: { id: string; nome: string };
  temporada: { id: string; ano: number; status: string; cadastroFasesEncerrado: boolean; tabelaGerada: boolean };
  numeroTurnos: number | null;
  numeroGrupos: number | null;
  distribuicao: DistribuicaoEquipes | null;
  /** Distribuição manual: a diretoria já escolheu o grupo de cada vaga. */
  distribuicaoManualDefinida: boolean;
  jogosPorConfronto: number | null;
  numeroConfrontos: number | null;
  classificadosPrimeiros: number | null;
  melhoresExtras: number;
  faseAnteriorNome: string | null;
  equipesDaCategoria: EquipeResumo[];
  permiteEditarCruzamentos: boolean;
  /** Verdadeiro enquanto a tabela de jogos não foi gerada: o que se vê é uma simulação. */
  previa: boolean;
  previaErro: string | null;
  grupos: GrupoDaFase[];
  vagas: VagaDaFase[];
  confrontos: ConfrontoDaFase[];
  confrontosPendentes: number[];
  fasesAnteriores: FaseAnteriorOpcao[];
  jogos: JogoDaFase[];
  totalJogos: number;
}

export interface DadosConfronto {
  nome: string | null;
  origemA: Omit<Origem, 'texto'>;
  origemB: Omit<Origem, 'texto'>;
}

export interface DadosAgendamento {
  /** yyyy-MM-dd */
  data: string | null;
  /** HH:mm */
  hora: string | null;
  localId: string | null;
}

export function obterDetalheFase(faseId: string): Promise<DetalheFase> {
  return obter<DetalheFase>(`/api/fases/${faseId}`, 'Erro ao carregar a fase');
}

export function definirDistribuicaoManual(faseId: string, grupos: number[]): Promise<void> {
  return enviar('PUT', `/api/fases/${faseId}/distribuicao-manual`, { grupos }, 'Erro ao salvar a distribuição dos grupos');
}

export function definirConfronto(faseId: string, numero: number, dados: DadosConfronto): Promise<void> {
  return enviar('PUT', `/api/fases/${faseId}/confrontos/${numero}`, dados, 'Erro ao salvar o confronto');
}

export function gerarTabelaJogos(temporadaId: string): Promise<void> {
  return enviar('POST', `/api/temporadas/${temporadaId}/tabela-jogos`, undefined, 'Erro ao montar a tabela de jogos');
}

export function agendarJogo(jogoId: string, dados: DadosAgendamento): Promise<void> {
  return enviar('PATCH', `/api/jogos/${jogoId}`, dados, 'Erro ao agendar o jogo');
}

export function listarLocais(associacaoId: string): Promise<LocalItem[]> {
  return obter<LocalItem[]>(`/api/associacoes/${associacaoId}/locais`, 'Erro ao carregar os locais');
}

export function editarLocal(localId: string, dados: { nome: string; cidade: string; estado: string | null }): Promise<void> {
  return enviar('PATCH', `/api/locais/${localId}`, dados, 'Erro ao editar o local');
}

export function excluirLocal(localId: string): Promise<void> {
  return enviar('DELETE', `/api/locais/${localId}`, undefined, 'Erro ao excluir o local');
}

export function criarLocal(associacaoId: string, dados: { nome: string; cidade: string; estado: string | null }): Promise<void> {
  return enviar('POST', `/api/associacoes/${associacaoId}/locais`, dados, 'Erro ao cadastrar o local');
}

export interface LadoLista {
  texto: string;
  /** Falso enquanto a equipe é só uma referência ("1º do Grupo A"). */
  definida: boolean;
  equipeId: string | null;
}

export interface JogoLista {
  id: string;
  numero: number;
  rodada: number;
  jogoDaSerie: number | null;
  opcional: boolean;
  categoria: { id: string; nome: string };
  fase: { id: string; nome: string; tipo: TipoFase };
  /** "R3" em pontos corridos/grupos; "Final · J2" no mata-mata. */
  etiqueta: string;
  casa: LadoLista;
  visitante: LadoLista;
  data: string | null;
  hora: string | null;
  local: LocalItem | null;
  status: StatusJogo;
  placarCasa: number | null;
  placarVisitante: number | null;
}

export interface JogosDaTemporada {
  id: string;
  ano: number;
  status: string;
  associacaoId: string;
  tabelaGerada: boolean;
  categorias: { id: string; nome: string }[];
  fases: { id: string; nome: string; ordem: number; temporadaCategoriaId: string }[];
  jogos: JogoLista[];
}

export interface DadosLote {
  jogoIds: string[];
  /** yyyy-MM-dd */
  data: string;
  /** HH:mm */
  horaInicial: string;
  intervaloMinutos: number;
  localId: string | null;
}

export function obterJogosDaTemporada(temporadaId: string): Promise<JogosDaTemporada> {
  return obter<JogosDaTemporada>(`/api/temporadas/${temporadaId}/jogos`, 'Erro ao carregar os jogos');
}

export function agendarEmLote(temporadaId: string, dados: DadosLote): Promise<void> {
  return enviar('PATCH', `/api/temporadas/${temporadaId}/jogos/agendar-lote`, dados, 'Erro ao agendar os jogos');
}

// ---------- Tela 10: detalhe do jogo ----------

export type StatusSumula = 'EmPreparacao' | 'EmAndamento' | 'Intervalo' | 'Encerrada';
export type LadoTime = 'Casa' | 'Visitante';

export interface LadoJogo {
  texto: string;
  /** Falso enquanto a equipe é só uma referência. */
  definida: boolean;
  equipeId: string | null;
  cor: string | null;
}

export interface ResumoTimeSumula {
  lado: LadoTime;
  nome: string;
  relacionados: number;
  tecnico: string | null;
  capitao: string | null;
}

export interface DetalheJogo {
  id: string;
  numero: number;
  rodada: number;
  jogoDaSerie: number | null;
  opcional: boolean;
  categoria: { id: string; nome: string };
  fase: { id: string; nome: string; tipo: TipoFase };
  etiqueta: string;
  temporada: { id: string; ano: number; status: string };
  associacaoId: string;
  casa: LadoJogo;
  visitante: LadoJogo;
  data: string | null;
  hora: string | null;
  local: LocalItem | null;
  status: StatusJogo;
  placarCasa: number | null;
  placarVisitante: number | null;
  /** Só no W.O.: a equipe ausente é a da casa? */
  woCasaAusente: boolean | null;
  podeAgendar: boolean;
  podeRegistrarWO: boolean;
  podePrepararSumula: boolean;
  motivoSemSumula: string | null;
  sumula: { id: string; status: StatusSumula; times: ResumoTimeSumula[] } | null;
}

export function obterDetalheJogo(jogoId: string): Promise<DetalheJogo> {
  return obter<DetalheJogo>(`/api/jogos/${jogoId}`, 'Erro ao carregar o jogo');
}

export function registrarWO(jogoId: string, casaAusente: boolean): Promise<void> {
  return enviar('POST', `/api/jogos/${jogoId}/wo`, { casaAusente }, 'Erro ao registrar o W.O.');
}

// ---------- Tela 11A: preparar a súmula ----------

export interface AtletaElenco {
  atletaId: string;
  nome: string;
}

export interface JogadorRelacionado {
  id: string;
  atletaId: string;
  nome: string;
  numero: string;
  titular: boolean;
  /** Período em que foi acrescentado depois do início do jogo (nulo = relacionado desde o começo). */
  chegouNoPeriodo: number | null;
}

export interface TimePreparo {
  lado: LadoTime;
  equipeId: string;
  nome: string;
  cor: string | null;
  tecnico: string | null;
  auxiliarTecnico: string | null;
  capitaoAtletaId: string | null;
  elenco: AtletaElenco[];
  jogadores: JogadorRelacionado[];
  pendencias: string[];
}

export interface PreparoSumula {
  jogo: { id: string; numero: number; status: StatusJogo; data: string | null; hora: string | null; categoria: string; fase: string };
  existe: boolean;
  sumula: { id: string; status: StatusSumula; editavel: boolean; podeAcrescentarAtletas: boolean; placarCasa: number; placarVisitante: number } | null;
  times: TimePreparo[];
  /** Sem rodízio na categoria, vale só o mínimo da FIBA. */
  categoriaComRodizio: boolean;
  regras: { maximo: number; minimoParaIniciar: number; titulares: number; minimoParaRodizio: number };
}

export interface DadosRelacao {
  lado: LadoTime;
  tecnico: string | null;
  auxiliarTecnico: string | null;
  capitaoAtletaId: string | null;
  jogadores: { atletaId: string; numero: string; titular: boolean }[];
}

export function obterPreparoSumula(jogoId: string): Promise<PreparoSumula> {
  return obter<PreparoSumula>(`/api/jogos/${jogoId}/sumula`, 'Erro ao carregar a súmula');
}

export function prepararSumula(jogoId: string): Promise<void> {
  return enviar('POST', `/api/jogos/${jogoId}/sumula`, undefined, 'Erro ao preparar a súmula');
}

export function salvarRelacao(jogoId: string, dados: DadosRelacao): Promise<void> {
  return enviar('PUT', `/api/jogos/${jogoId}/sumula/relacao`, dados, 'Erro ao salvar a relação');
}

/** Acrescenta à súmula, depois do início do jogo, um atleta do elenco que chegou atrasado (entra no banco). */
export function acrescentarAtleta(sumulaId: string, dados: { lado: LadoTime; atletaId: string; numero: string }): Promise<void> {
  return enviar('POST', `/api/sumulas/${sumulaId}/relacionados`, dados, 'Erro ao acrescentar o atleta');
}

export function iniciarSumula(jogoId: string): Promise<void> {
  return enviar('POST', `/api/jogos/${jogoId}/sumula/iniciar`, undefined, 'Erro ao iniciar a súmula');
}

/** Telas estáticas do mesário e do placar (usam o id da súmula como "partidaId"). */
export const linksDaSumula = (sumulaId: string) => ({
  mesarioTempo: `/arbitros/mesario-tempo.html?partidaId=${sumulaId}`,
  mesarioStats: `/arbitros/mesario-stats.html?partidaId=${sumulaId}`,
  placar: `/arbitros/placar.html?partidaId=${sumulaId}`,
});

// ---- Classificação da fase (Tela 12) ----

export type Classificado = 'Direto' | 'Melhor' | null;

export interface LinhaClassificacao {
  posicao: number;
  vagaId: string;
  equipe: EquipeResumo | null;
  texto: string;
  jogos: number;
  vitorias: number;
  derrotas: number;
  derrotasWO: number;
  pontosPorVitorias: number;
  pontosPorDerrotas: number;
  pontosPorWO: number;
  subtotal: number;
  bonificacao: number;
  total: number;
  pontosFeitos: number;
  pontosSofridos: number;
  saldo: number;
  /** Últimos resultados, "V"/"D", do mais antigo para o mais recente. */
  sequencia: string;
  empatePorSorteio: boolean;
  /** A diretoria já registrou o sorteio deste empate. */
  sorteioDefinido: boolean;
  classificado: Classificado;
}

export interface LadoSerie {
  equipe: EquipeResumo | null;
  texto: string;
  vitorias: number;
  venceu: boolean;
}

export interface ConfrontoClassificacao {
  id: string;
  numero: number;
  nome: string;
  decidido: boolean;
  iniciado: boolean;
  a: LadoSerie;
  b: LadoSerie;
  jogos: {
    id: string; numero: number; jogoDaSerie: number | null; opcional: boolean; status: StatusJogo;
    placarCasa: number | null; placarVisitante: number | null;
  }[];
}

export type CorpoClassificacao =
  | { tipo: 'Tabela'; definitiva: boolean; podeDefinirSorteio: boolean; sorteioDefinido: boolean; linhas: LinhaClassificacao[] }
  | {
      tipo: 'Grupos'; definitiva: boolean;
      grupos: { ordem: number; nome: string; definitiva: boolean; podeDefinirSorteio: boolean; sorteioDefinido: boolean; linhas: LinhaClassificacao[] }[];
      melhores: { posicao: number; grupo: string; linha: LinhaClassificacao }[];
    }
  | { tipo: 'MataMata'; vitoriasNecessarias: number; confrontos: ConfrontoClassificacao[] };

export interface ClassificacaoFase {
  id: string;
  nome: string;
  ordem: number;
  tipo: TipoFase;
  status: StatusFase;
  categoria: { id: string; nome: string };
  temporada: { id: string; ano: number; tabelaGerada: boolean; valorBonificacaoPorAtleta: number };
  numeroTurnos: number | null;
  numeroGrupos: number | null;
  jogosPorConfronto: number | null;
  classificadosPrimeiros: number | null;
  melhoresExtras: number;
  totalJogos: number;
  jogosEncerrados: number;
  bonificacaoAtiva: boolean;
  /** Nulo enquanto a tabela de jogos não foi gerada. */
  classificacao: CorpoClassificacao | null;
}

export function obterClassificacaoFase(faseId: string): Promise<ClassificacaoFase> {
  return obter<ClassificacaoFase>(`/api/fases/${faseId}/classificacao`, 'Erro ao carregar a classificação');
}

/** Registra o sorteio da diretoria para um empate sem critério (ids das vagas, da melhor para a pior colocação). */
export function definirSorteio(faseId: string, grupoOrdem: number | null, ordem: string[]): Promise<void> {
  return enviar('PUT', `/api/fases/${faseId}/sorteio`, { grupoOrdem, ordem }, 'Erro ao registrar o sorteio');
}

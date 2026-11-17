import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { encerrarInscricoes, encerrarTemporada, iniciarTemporada } from '../../api/campeonatos';
import { type Categoria, obterCategoriasDaAssociacao } from '../../api/categorias';
import {
  type ResumoTemporada, type TemporadaDetalhe, type TipoDesconto,
  adicionarCategoria, alterarDatas, alterarInscricoesHabilitadas, configurarBonificacao, definirDesconto,
  definirPublico,
  definirTaxaInscricao, definirValorCategoria, gerarCobrancas, obterResumo, obterTemporada, removerCategoria, removerDesconto,
} from '../../api/temporadas';
import DataInput, { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import { listarEquipesInscritas } from '../../api/equipesInscritas';
import { formatarData, formatarMoeda, paraNumero } from '../../utils/formatacao';
import styles from './DetalheTemporada.module.css';

type Status = 'InscricoesAbertas' | 'InscricoesEncerradas' | 'EmAndamento' | 'Encerrada';
const ORDEM: Status[] = ['InscricoesAbertas', 'InscricoesEncerradas', 'EmAndamento', 'Encerrada'];
const ROTULO: Record<Status, string> = {
  InscricoesAbertas: 'Inscrições abertas',
  InscricoesEncerradas: 'Inscrições encerradas',
  EmAndamento: 'Em andamento',
  Encerrada: 'Encerrada',
};
const PILL: Record<Status, string> = {
  InscricoesAbertas: styles.pillAberta,
  InscricoesEncerradas: styles.pillEncerradaInsc,
  EmAndamento: styles.pillEmAndamento,
  Encerrada: styles.pillEncerrada,
};
const PROXIMO: Partial<Record<Status, string>> = {
  InscricoesAbertas: 'Encerrar inscrições',
  InscricoesEncerradas: 'Iniciar temporada',
  EmAndamento: 'Encerrar temporada',
};

type Nivel = 'ok' | 'aviso' | 'bloqueio';
interface Condicao { nivel: Nivel; texto: string }

const isoParaDdmm = (iso: string | null | undefined) => (iso ? formatarData(iso) : '');
const dinheiro = (v: number | null) => (v == null ? '' : v.toFixed(2).replace('.', ','));

type Modal = 'avanco' | 'categoria' | 'desconto' | null;

export default function DetalheTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [temporada, setTemporada] = useState<TemporadaDetalhe | null>(null);
  const [resumo, setResumo] = useState<ResumoTemporada | null>(null);
  /** Torneio aberto: quantas equipes foram inscritas pelo site e quantas esperam a diretoria. */
  const [equipesInscritas, setEquipesInscritas] = useState<{ total: number; pendentes: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [modal, setModal] = useState<Modal>(null);
  const [aRemover, setARemover] = useState<{ categoriaId: string; nome: string } | null>(null);

  // campos editáveis
  const [datas, setDatas] = useState({ inscIni: '', inscFim: '', compIni: '', compFim: '' });
  const [erroDatas, setErroDatas] = useState('');
  const [taxa, setTaxa] = useState('');
  const [bonif, setBonif] = useState('');
  const [valores, setValores] = useState<Record<string, string>>({});

  // modais
  const [catalogo, setCatalogo] = useState<Categoria[]>([]);
  const [novaCat, setNovaCat] = useState('');
  const [desc, setDesc] = useState<{ ids: string[]; tipo: TipoDesconto; valor: string }>({ ids: [], tipo: 'Percentual', valor: '' });
  const [erroModal, setErroModal] = useState('');

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      const [t, r] = await Promise.all([obterTemporada(id), obterResumo(id)]);
      setTemporada(t);
      setResumo(r);
      if (t.publico === 'Aberto') {
        const e = await listarEquipesInscritas(t.id).catch(() => null);
        setEquipesInscritas(e ? { total: e.equipes.length, pendentes: e.equipes.filter(x => x.situacao === 'EmAnalise' || x.situacao === 'AguardandoConfirmacoes').length } : null);
      } else setEquipesInscritas(null);
      setDatas({ inscIni: isoParaDdmm(t.dataInicioInscricoes), inscFim: isoParaDdmm(t.dataFimInscricoes), compIni: isoParaDdmm(t.dataInicioCompeticao), compFim: isoParaDdmm(t.dataFimCompeticao) });
      setTaxa(dinheiro(t.taxaInscricao));
      setBonif(String(t.valorBonificacaoPorAtleta ?? 0).replace('.', ','));
      setValores(Object.fromEntries(t.categorias.map(c => [c.categoriaId, dinheiro(c.valor)])));
      setErro('');
    } catch {
      setErro('Não foi possível carregar a temporada.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { void carregar(); }, [carregar]);

  /** Executa uma ação, mostra o resultado e recarrega os números. */
  async function executar(acao: () => Promise<void>, sucesso: string) {
    setSalvando(true);
    setAviso(null);
    try {
      await acao();
      setAviso({ tipo: 'ok', texto: sucesso });
      await carregar();
      return true;
    } catch (e) {
      setAviso({ tipo: 'erro', texto: e instanceof Error ? e.message : 'Não foi possível concluir a ação.' });
      return false;
    } finally {
      setSalvando(false);
    }
  }

  const status = (temporada?.status ?? 'InscricoesAbertas') as Status;
  const encerrada = status === 'Encerrada';

  const condicoes = useMemo<Condicao[]>(() => {
    if (!temporada || !resumo) return [];
    const cats = temporada.categorias;
    const lista: Condicao[] = [];
    if (status === 'InscricoesAbertas') {
      lista.push(resumo.pedidosPendentes === 0
        ? { nivel: 'ok', texto: 'Nenhum pedido de inscrição pendente.' }
        : { nivel: 'aviso', texto: `${resumo.pedidosPendentes} pedidos pendentes de análise. Aprove ou recuse antes de encerrar.` });
      lista.push(cats.length > 0
        ? { nivel: 'ok', texto: `${cats.length} ${cats.length === 1 ? 'categoria' : 'categorias'} na temporada.` }
        : { nivel: 'bloqueio', texto: 'Adicione ao menos uma categoria.' });
    } else if (status === 'InscricoesEncerradas') {
      const semValor = cats.filter(c => c.valor == null);
      lista.push(semValor.length === 0
        ? { nivel: 'ok', texto: 'Valor definido em todas as categorias.' }
        : { nivel: 'aviso', texto: `Falta o valor de: ${semValor.map(c => c.nome).join(', ')}.` });
      lista.push(resumo.cobranca.geradaEm
        ? { nivel: 'ok', texto: 'Cobranças geradas.' }
        : { nivel: 'aviso', texto: 'Cobranças ainda não geradas.' });
      lista.push(temporada.categorias.length > 0 && resumo.jogosTotal > 0
        ? { nivel: 'ok', texto: 'Tabela de jogos gerada.' }
        : { nivel: 'bloqueio', texto: 'Gere a tabela de jogos (equipes e fases) antes de iniciar.' });
    } else if (status === 'EmAndamento') {
      lista.push(resumo.jogosEmAberto === 0
        ? { nivel: 'ok', texto: 'Todos os jogos estão encerrados.' }
        : { nivel: 'bloqueio', texto: `${resumo.jogosEmAberto} ${resumo.jogosEmAberto === 1 ? 'jogo ainda agendado ou em andamento' : 'jogos ainda agendados ou em andamento'}.` });
      lista.push({ nivel: 'aviso', texto: 'Encerrar fecha a temporada: valores, equipes e fases deixam de poder ser alterados.' });
    }
    return lista;
  }, [temporada, resumo, status]);

  const bloqueado = condicoes.some(c => c.nivel === 'bloqueio');

  async function avancar() {
    if (!temporada) return;
    const { campeonatoId, id: tid } = temporada;
    const acao = status === 'InscricoesAbertas' ? () => encerrarInscricoes(campeonatoId, tid)
      : status === 'InscricoesEncerradas' ? () => iniciarTemporada(campeonatoId, tid)
        : () => encerrarTemporada(campeonatoId, tid);
    setModal(null);
    await executar(acao, `Situação alterada: ${PROXIMO[status]}.`);
  }

  function salvarDatas() {
    if (!temporada) return;
    setErroDatas('');
    if (!dataValida(datas.inscIni) || !dataValida(datas.inscFim)) return setErroDatas('Informe as duas datas das inscrições (DD/MM/AAAA).');
    if (ddmmParaIso(datas.inscFim) <= ddmmParaIso(datas.inscIni)) return setErroDatas('O fim das inscrições deve ser depois do início.');
    if (datas.compIni && !dataValida(datas.compIni)) return setErroDatas('Início da competição inválido (DD/MM/AAAA).');
    if (datas.compFim && !dataValida(datas.compFim)) return setErroDatas('Fim da competição inválido (DD/MM/AAAA).');
    if (dataValida(datas.compIni) && dataValida(datas.compFim) && ddmmParaIso(datas.compFim) < ddmmParaIso(datas.compIni))
      return setErroDatas('O fim da competição não pode ser antes do início.');
    void executar(() => alterarDatas(temporada.id, {
      dataInicioInscricoes: ddmmParaIso(datas.inscIni),
      dataFimInscricoes: ddmmParaIso(datas.inscFim),
      dataInicioCompeticao: dataValida(datas.compIni) ? ddmmParaIso(datas.compIni) : undefined,
      dataFimCompeticao: dataValida(datas.compFim) ? ddmmParaIso(datas.compFim) : undefined,
    }), 'Datas salvas.');
  }

  function salvarTaxa() {
    if (!temporada) return;
    const n = taxa.trim() === '' ? null : paraNumero(taxa);
    if (n !== null && (Number.isNaN(n) || n < 0)) return setAviso({ tipo: 'erro', texto: 'Informe um valor válido para a taxa.' });
    void executar(() => definirTaxaInscricao(temporada.id, n), 'Taxa salva.');
  }

  function salvarBonificacao() {
    if (!temporada) return;
    const n = bonif.trim() === '' ? 0 : paraNumero(bonif);
    if (Number.isNaN(n) || n < 0) return setAviso({ tipo: 'erro', texto: 'Informe um valor válido para a bonificação.' });
    void executar(() => configurarBonificacao(temporada.campeonatoId, temporada.id, n), 'Bonificação salva.');
  }

  function salvarValor(categoriaId: string, nome: string, atual: number | null) {
    if (!temporada) return;
    const texto = valores[categoriaId] ?? '';
    const n = texto.trim() === '' ? null : paraNumero(texto);
    if (n !== null && (Number.isNaN(n) || n < 0)) return setAviso({ tipo: 'erro', texto: `Valor inválido para ${nome}.` });
    if (n === atual) return;
    void executar(() => definirValorCategoria(temporada.id, categoriaId, n), `Valor de ${nome} salvo.`);
  }

  async function abrirNovaCategoria() {
    if (!temporada) return;
    setNovaCat('');
    setErroModal('');
    setModal('categoria');
    obterCategoriasDaAssociacao(temporada.associacaoId).then(setCatalogo).catch(() => setCatalogo([]));
  }

  async function confirmarNovaCategoria() {
    if (!temporada) return;
    if (!novaCat) return setErroModal('Selecione uma categoria.');
    setSalvando(true);
    try {
      await adicionarCategoria(temporada.id, { categoriaId: novaCat });
      setModal(null);
      setAviso({ tipo: 'ok', texto: 'Categoria adicionada.' });
      await carregar();
    } catch (e) {
      setErroModal(e instanceof Error ? e.message : 'Erro ao adicionar categoria.');
    } finally {
      setSalvando(false);
    }
  }

  async function confirmarRemocao() {
    if (!temporada || !aRemover) return;
    const alvo = aRemover;
    setARemover(null);
    await executar(() => removerCategoria(temporada.id, alvo.categoriaId), 'Categoria removida.');
  }

  function abrirDesconto() {
    setDesc({ ids: [], tipo: 'Percentual', valor: '' });
    setErroModal('');
    setModal('desconto');
  }

  async function salvarDesconto() {
    if (!temporada) return;
    const v = paraNumero(desc.valor);
    if (desc.ids.length < 2) return setErroModal('Marque ao menos 2 categorias.');
    if (!(v > 0)) return setErroModal('Informe um valor maior que zero.');
    if (desc.tipo === 'Percentual' && v > 100) return setErroModal('O percentual não pode passar de 100%.');
    setSalvando(true);
    try {
      await definirDesconto(temporada.id, { temporadaCategoriaIds: desc.ids, tipo: desc.tipo, valor: v });
      setModal(null);
      setAviso({ tipo: 'ok', texto: 'Desconto salvo.' });
      await carregar();
    } catch (e) {
      setErroModal(e instanceof Error ? e.message : 'Erro ao salvar o desconto.');
    } finally {
      setSalvando(false);
    }
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !temporada || !resumo) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;

  const habilitadas = temporada.inscricoesHabilitadas;
  const cobranca = resumo.cobranca;
  const cobrancaGerada = cobranca.geradaEm != null;
  const faltaValor = temporada.categorias.some(c => c.valor == null);
  const catsDisponiveis = catalogo.filter(c => !temporada.categorias.some(tc => tc.categoriaId === c.id));
  const indice = ORDEM.indexOf(status);

  const alternarInscricoes = () => {
    if (encerrada) return;
    void executar(
      () => alterarInscricoesHabilitadas(temporada.campeonatoId, temporada.id, !habilitadas),
      habilitadas ? 'Inscrições desabilitadas.' : 'Inscrições habilitadas.');
  };

  const tiles: { n: number; rotulo: string; chip: [string, string]; ir: string }[] = [
    { n: resumo.fichas, rotulo: 'Fichas de inscrição', ir: `/temporadas/${temporada.id}/inscricoes`,
      chip: resumo.pedidosPendentes > 0 ? [`${resumo.pedidosPendentes} pendentes`, styles.miniAviso] : ['em dia', styles.miniOk] },
    ...(equipesInscritas ? [{ n: equipesInscritas.total, rotulo: 'Equipes inscritas', ir: `/temporadas/${temporada.id}/equipes-inscritas`,
      chip: (equipesInscritas.pendentes > 0 ? [`${equipesInscritas.pendentes} para analisar`, styles.miniAviso] : ['em dia', styles.miniOk]) as [string, string] }] : []),
    { n: resumo.equipes, rotulo: 'Equipes', ir: `/temporadas/${temporada.id}/equipes`,
      chip: status === 'InscricoesAbertas' ? ['após encerrar inscrições', ''] : resumo.equipes > 0 ? ['formadas', styles.miniOk] : ['a formar', styles.miniAviso] },
    { n: resumo.fases, rotulo: 'Fases', ir: `/temporadas/${temporada.id}/fases`,
      chip: resumo.jogosTotal > 0 ? ['cadastro encerrado', styles.miniOk] : ['em montagem', ''] },
    { n: resumo.jogosTotal, rotulo: 'Jogos', ir: `/temporadas/${temporada.id}/jogos`,
      chip: resumo.jogosTotal === 0 ? ['tabela não gerada', '']
        : resumo.jogosEmAberto > 0 ? [`${resumo.jogosTotal - resumo.jogosEmAberto} encerrados`, styles.miniInfo] : ['todos encerrados', styles.miniOk] },
  ];

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/campeonatos/${temporada.campeonatoId}`)}>‹ Voltar</button>
      </div>

      {aviso && (
        <div className={`${styles.faixa} ${aviso.tipo === 'ok' ? styles.faixaOk : styles.faixaErro}`} role={aviso.tipo === 'ok' ? 'status' : 'alert'}>
          {aviso.texto}
        </div>
      )}

      {/* Ciclo de vida */}
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div>
            <h1 className={styles.titulo}>Temporada {temporada.ano}</h1>
            <p className={styles.datas}>
              Inscrições: {formatarData(temporada.dataInicioInscricoes)} – {formatarData(temporada.dataFimInscricoes)}
              {temporada.dataInicioCompeticao && ` · Competição: ${formatarData(temporada.dataInicioCompeticao)}${temporada.dataFimCompeticao ? ` – ${formatarData(temporada.dataFimCompeticao)}` : ''}`}
            </p>
          </div>
          <span className={`${styles.pill} ${PILL[status]}`}>{ROTULO[status]}</span>
        </div>

        <div className={styles.steps}>
          {ORDEM.map((s, k) => (
            <div key={s} data-n={k + 1} className={`${styles.step} ${k < indice ? styles.stepFeita : k === indice ? styles.stepAgora : ''}`}>{ROTULO[s]}</div>
          ))}
        </div>

        {encerrada ? (
          <div className={`${styles.faixa} ${styles.faixaInfo}`}>Temporada encerrada. Nada mais pode ser alterado; o histórico fica disponível para consulta.</div>
        ) : (
          <div className={styles.avanco}>
            <div className={styles.avancoLista}>
              <h3 className={styles.h3}>Antes de avançar</h3>
              <ul className={styles.check}>
                {condicoes.map((c, i) => (
                  <li key={i}>
                    <span className={`${styles.icone} ${c.nivel === 'ok' ? styles.iconeOk : c.nivel === 'bloqueio' ? styles.iconeBloqueio : styles.iconeAviso}`}>
                      {c.nivel === 'ok' ? '✓' : c.nivel === 'bloqueio' ? '✕' : '!'}
                    </span>
                    <span>{c.texto}{c.nivel !== 'ok' && <span className={styles.etiqueta}>({c.nivel === 'bloqueio' ? 'bloqueia' : 'aviso'})</span>}</span>
                  </li>
                ))}
              </ul>
            </div>
            <div className={styles.avancoBotao}>
              <button className={styles.btnPrimary} disabled={bloqueado || salvando} onClick={() => setModal('avanco')}>{PROXIMO[status]}</button>
              {bloqueado && <span className={styles.dica}>Resolva os itens que bloqueiam.</span>}
            </div>
          </div>
        )}
      </div>

      {/* Andamento */}
      <div className={styles.card}>
        <div className={styles.sectionHeader}>
          <div><h2 className={styles.h2}>Andamento</h2><p className={styles.dica}>Cada bloco abre a tela daquela etapa.</p></div>
        </div>
        <div className={styles.tiles}>
          {tiles.map(t => (
            <button key={t.rotulo} className={styles.tile} onClick={() => navigate(t.ir)}>
              <span className={styles.tileN}>{t.n}</span>
              <span className={styles.tileL}>{t.rotulo}</span>
              <span className={styles.tileS}><span className={`${styles.mini} ${t.chip[1]}`}>{t.chip[0]}</span></span>
            </button>
          ))}
        </div>
      </div>

      <div className={styles.grid2}>
        {/* Inscrições e datas */}
        <div className={styles.card}>
          <div className={styles.sectionHeader}>
            <div><h2 className={styles.h2}>Datas e inscrições</h2><p className={styles.dica}>As datas da competição são opcionais.</p></div>
          </div>
          <div className={styles.linha}>
            <div className={styles.field}><label htmlFor="d-ii">Início das inscrições</label><DataInput id="d-ii" anoMin={2000} anoMax={2100} value={datas.inscIni} onChange={v => setDatas(d => ({ ...d, inscIni: v }))} /></div>
            <div className={styles.field}><label htmlFor="d-if">Fim das inscrições</label><DataInput id="d-if" anoMin={2000} anoMax={2100} value={datas.inscFim} onChange={v => setDatas(d => ({ ...d, inscFim: v }))} /></div>
          </div>
          <div className={styles.linha}>
            <div className={styles.field}><label htmlFor="d-ci">Início da competição <small>(opcional)</small></label><DataInput id="d-ci" anoMin={2000} anoMax={2100} value={datas.compIni} onChange={v => setDatas(d => ({ ...d, compIni: v }))} /></div>
            <div className={styles.field}><label htmlFor="d-cf">Fim da competição <small>(opcional)</small></label><DataInput id="d-cf" anoMin={2000} anoMax={2100} value={datas.compFim} onChange={v => setDatas(d => ({ ...d, compFim: v }))} /></div>
          </div>
          {erroDatas && <p className={styles.fieldErro}>{erroDatas}</p>}
          <button className={styles.btnSecondary} onClick={salvarDatas} disabled={encerrada || salvando}>Salvar datas</button>

          <div className={styles.field} style={{ marginTop: 16 }}>
            <label htmlFor="t-publico">Público do campeonato</label>
            <select id="t-publico" className={styles.select} value={temporada.publico} disabled={encerrada || salvando}
              onChange={e => void executar(() => definirPublico(temporada.id, e.target.value as 'Associados' | 'Aberto'),
                e.target.value === 'Aberto' ? 'Campeonato aberto: qualquer pessoa pode se inscrever e participar não cria vínculo de associado.' : 'Só associados: a inscrição efetivada cria o vínculo de associado na temporada.')}>
              <option value="Associados">Só associados</option>
              <option value="Aberto">Aberto (qualquer pessoa)</option>
            </select>
            <small className={styles.dica}>Em campeonato aberto, o site oferece a inscrição de equipe e a individual, e participar <b>não torna a pessoa associada</b>.</small>
          </div>

          <div className={styles.switch} style={{ marginTop: 16 }}>
            <button className={`${styles.sw} ${habilitadas ? styles.swOn : ''}`} onClick={alternarInscricoes}
              disabled={encerrada || salvando || (!habilitadas && cobrancaGerada)} aria-label="Habilitar inscrições" aria-pressed={habilitadas}
              title={!habilitadas && cobrancaGerada ? 'Com as cobranças geradas não é possível reabrir as inscrições' : ''} />
            <div className={styles.switchTexto}>
              <b>{habilitadas ? 'Inscrições habilitadas' : 'Inscrições desabilitadas'}</b>
              <span className={styles.dica}>
                {habilitadas ? 'Novas fichas estão sendo aceitas.'
                  : encerrada ? 'Temporada encerrada.'
                    : cobrancaGerada ? 'Cobranças já geradas: não dá para reabrir. A diretoria ainda pode inscrever alguém na tela de Inscrições.'
                      : 'Não entram novas fichas. Dá para reabrir enquanto não houver cobrança gerada. A diretoria pode inscrever alguém mesmo assim.'}
              </span>
            </div>
          </div>
        </div>

        {/* Financeiro */}
        <div className={styles.card}>
          <div className={styles.sectionHeader}>
            <div><h2 className={styles.h2}>Financeiro</h2><p className={styles.dica}>Valores que valem para toda a temporada.</p></div>
          </div>
          <div className={styles.linha}>
            <div className={styles.field}><label htmlFor="taxa">Taxa de inscrição (R$, 1× por ficha)</label>
              <input id="taxa" className={styles.input} inputMode="decimal" value={taxa} onChange={e => setTaxa(e.target.value)} placeholder="a definir" disabled={encerrada} /></div>
            <button className={styles.btnSecondary} onClick={salvarTaxa} disabled={encerrada || salvando}>Salvar</button>
          </div>
          <div className={styles.linha}>
            <div className={styles.field}><label htmlFor="bonif">Bonificação por atleta no rodízio (pontos)</label>
              <input id="bonif" className={styles.input} inputMode="decimal" value={bonif} onChange={e => setBonif(e.target.value)} disabled={encerrada} /></div>
            <button className={styles.btnSecondary} onClick={salvarBonificacao} disabled={encerrada || salvando}>Salvar</button>
          </div>
          <p className={styles.dica} style={{ marginTop: -6, marginBottom: 12 }}>0 desliga a bonificação.</p>

          <h3 className={styles.h3}>Cobranças</h3>
          {cobrancaGerada ? (
            <div className={`${styles.faixa} ${styles.faixaOk}`}>
              <b>Cobranças geradas em {formatarData(cobranca.geradaEm!)}.</b> {cobranca.fichasComCobranca} fichas · previsto {formatarMoeda(cobranca.previsto)} · recebido {formatarMoeda(cobranca.recebido)}.
            </div>
          ) : (
            <div className={`${styles.faixa} ${habilitadas ? styles.faixaAviso : styles.faixaInfo}`}>
              {habilitadas ? 'Desabilite as inscrições antes de gerar as cobranças.' : 'Pronto para gerar: calcula o valor final de cada ficha (valor das categorias − desconto).'}
            </div>
          )}
          {cobrancaGerada && cobranca.fichasSemCobranca > 0 && (
            <div className={`${styles.faixa} ${styles.faixaAviso}`}>
              {cobranca.fichasSemCobranca} {cobranca.fichasSemCobranca === 1 ? 'ficha entrou' : 'fichas entraram'} depois das cobranças. Recalcule para incluí-las.
            </div>
          )}
          <button className={styles.btnPrimary} disabled={habilitadas || encerrada || faltaValor || salvando}
            onClick={() => void executar(() => gerarCobrancas(temporada.id), cobrancaGerada ? 'Cobranças recalculadas.' : 'Cobranças geradas.')}>
            {cobrancaGerada ? 'Recalcular cobranças' : 'Gerar cobranças'}
          </button>
          {faltaValor && !habilitadas && !encerrada && <p className={styles.dica}>Defina o valor de todas as categorias para gerar.</p>}
        </div>
      </div>

      {/* Categorias */}
      <div className={styles.card} style={{ marginTop: '1.25rem' }}>
        <div className={styles.sectionHeader}>
          <div>
            <h2 className={styles.h2}>Categorias da temporada <span className={styles.badge}>{temporada.categorias.length}</span></h2>
            <p className={styles.dica}>O valor de cada categoria é definido depois de encerradas as inscrições.</p>
          </div>
          <button className={styles.btnPrimary} onClick={abrirNovaCategoria} disabled={status !== 'InscricoesAbertas'}
            title={status !== 'InscricoesAbertas' ? 'Categorias só podem ser adicionadas com as inscrições abertas' : ''}>
            + Adicionar categoria
          </button>
        </div>
        <table className={styles.tabela}>
          <thead>
            <tr><th>Categoria</th><th>Regra</th><th className={styles.num}>Fichas</th><th className={styles.num}>Equipes</th><th className={styles.num}>Fases</th><th className={styles.num}>Valor (R$)</th><th /></tr>
          </thead>
          <tbody>
            {temporada.categorias.length === 0 && <tr><td colSpan={7} className={styles.mudo}>Nenhuma categoria. Adicione ao menos uma.</td></tr>}
            {temporada.categorias.map(c => {
              const pedidos = resumo.categorias[c.id]?.pedidos ?? 0;
              const eq = resumo.equipesPorCategoria[c.id] ?? 0;
              const fs = resumo.fasesPorCategoria[c.id] ?? 0;
              const motivos = [pedidos && `${pedidos} fichas`, eq && `${eq} equipes`, fs && `${fs} fases`].filter(Boolean).join(', ');
              const travada = encerrada || motivos !== '';
              const regra = `${c.idadeMinima > 0 ? `${c.idadeMinima}+ anos` : 'qualquer idade'} · ${c.sexo ?? 'mista'}${c.aceitaAbaixoIdadeMinima ? ' · aceita mais novos' : ''}`;
              return (
                <tr key={c.id}>
                  <td><b>{c.nome}</b></td>
                  <td className={styles.mudo}>{regra}</td>
                  <td className={styles.num}>{pedidos}</td>
                  <td className={styles.num}>{eq}</td>
                  <td className={styles.num}>{fs}</td>
                  <td className={styles.num}>
                    <input className={`${styles.input} ${styles.dinheiro}`} inputMode="decimal" placeholder="a definir" disabled={encerrada}
                      value={valores[c.categoriaId] ?? ''} aria-label={`Valor de ${c.nome}`}
                      onChange={e => setValores(v => ({ ...v, [c.categoriaId]: e.target.value }))}
                      onBlur={() => salvarValor(c.categoriaId, c.nome, c.valor)}
                      onKeyDown={e => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }} />
                  </td>
                  <td className={styles.num}>
                    <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} disabled={travada || salvando}
                      title={encerrada ? 'Temporada encerrada' : motivos ? `Não dá para remover: há ${motivos}` : 'Remover a categoria da temporada'}
                      onClick={() => setARemover({ categoriaId: c.categoriaId, nome: c.nome })}>Remover</button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Descontos */}
      <div className={styles.card}>
        <div className={styles.sectionHeader}>
          <div>
            <h2 className={styles.h2}>Descontos por combinação <span className={styles.badge}>{temporada.descontos.length}</span></h2>
            <p className={styles.dica}>Vale só para quem joga exatamente as categorias da combinação (2 ou mais).</p>
          </div>
          <button className={styles.btnPrimary} onClick={abrirDesconto} disabled={encerrada || temporada.categorias.length < 2}>+ Novo desconto</button>
        </div>
        {temporada.descontos.length === 0 ? (
          <p className={styles.mudo}>Sem descontos: cada ficha paga a soma dos valores das categorias.</p>
        ) : (
          <table className={styles.tabela}>
            <thead><tr><th>Combinação</th><th>Desconto</th><th /></tr></thead>
            <tbody>
              {temporada.descontos.map(d => (
                <tr key={d.id}>
                  <td><b>{d.categorias.join(' + ')}</b></td>
                  <td>− {d.tipo === 'Percentual' ? `${d.valor}%` : formatarMoeda(d.valor)}</td>
                  <td className={styles.num}>
                    <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} disabled={encerrada || salvando}
                      onClick={() => void executar(() => removerDesconto(temporada.id, d.id), 'Desconto removido.')}>Remover</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Modais */}
      {modal === 'avanco' && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModal(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>{PROXIMO[status]}</h2>
            <p className={styles.info}>
              {status === 'InscricoesAbertas' && 'As inscrições fecham e as equipes passam a poder ser formadas. Pedidos pendentes continuam podendo ser analisados.'}
              {status === 'InscricoesEncerradas' && 'A temporada entra em andamento e os jogos podem ser disputados.'}
              {status === 'EmAndamento' && 'A temporada é encerrada. Depois disso nada pode ser alterado.'}
            </p>
            <ul className={styles.check}>
              {condicoes.filter(c => c.nivel !== 'ok').map((c, i) => (
                <li key={i}><span className={`${styles.icone} ${styles.iconeAviso}`}>!</span><span>{c.texto}</span></li>
              ))}
            </ul>
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModal(null)}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={avancar}>Confirmar</button>
            </div>
          </div>
        </div>
      )}

      {aRemover && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setARemover(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Remover categoria</h2>
            <p className={styles.info}>Remover <b>{aRemover.nome}</b> desta temporada? Só é possível porque não há fichas, equipes nem fases nela.</p>
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setARemover(null)}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={confirmarRemocao}>Remover</button>
            </div>
          </div>
        </div>
      )}

      {modal === 'categoria' && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModal(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Adicionar categoria</h2>
            <div className={styles.field}>
              <label htmlFor="cat-select">Categoria da associação</label>
              <select id="cat-select" className={styles.select} value={novaCat} onChange={e => { setNovaCat(e.target.value); setErroModal(''); }}>
                <option value="">Selecione...</option>
                {catsDisponiveis.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
              </select>
              <span className={styles.dica}>Só aparecem as categorias que ainda não estão na temporada.</span>
            </div>
            {erroModal && <p className={styles.fieldErro}>{erroModal}</p>}
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModal(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={confirmarNovaCategoria} disabled={salvando}>{salvando ? 'Salvando...' : 'Adicionar'}</button>
            </div>
          </div>
        </div>
      )}

      {modal === 'desconto' && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModal(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Novo desconto por combinação</h2>
            <div className={styles.field}>
              <label>Categorias da combinação (marque 2 ou mais)</label>
              {temporada.categorias.map(c => (
                <label key={c.id} className={styles.cb}>
                  <input type="checkbox" checked={desc.ids.includes(c.id)}
                    onChange={() => setDesc(d => ({ ...d, ids: d.ids.includes(c.id) ? d.ids.filter(x => x !== c.id) : [...d.ids, c.id] }))} />
                  {c.nome}
                </label>
              ))}
            </div>
            <div className={styles.linha}>
              <div className={styles.field}>
                <label htmlFor="d-tipo">Tipo</label>
                <select id="d-tipo" className={styles.select} value={desc.tipo} onChange={e => setDesc(d => ({ ...d, tipo: e.target.value as TipoDesconto }))}>
                  <option value="Percentual">Percentual (%)</option>
                  <option value="Valor">Valor (R$)</option>
                </select>
              </div>
              <div className={styles.field}>
                <label htmlFor="d-valor">Valor</label>
                <input id="d-valor" className={styles.input} inputMode="decimal" placeholder="10" value={desc.valor} onChange={e => setDesc(d => ({ ...d, valor: e.target.value }))} />
              </div>
            </div>
            {erroModal && <p className={styles.fieldErro}>{erroModal}</p>}
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModal(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvarDesconto} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

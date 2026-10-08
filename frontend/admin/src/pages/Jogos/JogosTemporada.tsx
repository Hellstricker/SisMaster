import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  type JogoLista,
  type JogosDaTemporada,
  type LocalItem,
  type StatusJogo,
  agendarEmLote,
  listarLocais,
  obterJogosDaTemporada,
} from '../../api/jogos';
import DataInput, { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import { ModalAgendar } from '../Fases/DetalheFase';
import base from '../Fases/Fases.module.css';
import styles from './Jogos.module.css';

const STATUS_ROTULO: Record<StatusJogo, string> = {
  Agendado: 'Agendado', EmAndamento: 'Em andamento', Encerrado: 'Encerrado', WO: 'W.O.', Dispensado: 'Dispensado',
};
const STATUS_ESTILO: Record<StatusJogo, string> = {
  Agendado: base.stPlanejada, EmAndamento: base.stEmAndamento, Encerrado: base.stEncerrada, WO: base.stEncerrada, Dispensado: base.stPlanejada,
};
const DIAS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

const rotuloLocal = (l: { nome: string; cidade: string; estado: string | null }) => `${l.nome} — ${l.cidade}${l.estado ? `/${l.estado}` : ''}`;
const horaCurta = (h: string | null) => (h ? h.slice(0, 5) : '');

function rotuloDia(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return `${DIAS[new Date(y, m - 1, d).getDay()]}, ${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}/${y}`;
}

const minutosParaHora = (min: number) => `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`;

type Vista = 'data' | 'numero';

/**
 * Jogos da temporada: a tabela do campeonato inteira (categorias misturadas), por data e local ou por número,
 * com filtros e agendamento em lote.
 */
export default function JogosTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<JogosDaTemporada | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [vista, setVista] = useState<Vista>('data');
  const [filtro, setFiltro] = useState({ categoria: '', fase: '', status: '', local: '', busca: '', semData: false });
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set());
  const [agendando, setAgendando] = useState<JogoLista | null>(null);
  const [lote, setLote] = useState(false);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      setDados(await obterJogosDaTemporada(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os jogos.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(null), 4500);
    return () => clearTimeout(t);
  }, [aviso]);

  const jogos = useMemo(() => dados?.jogos ?? [], [dados]);

  const locaisDosJogos = useMemo(() => {
    const mapa = new Map<string, string>();
    for (const j of jogos) if (j.local) mapa.set(j.local.id, rotuloLocal(j.local));
    return [...mapa.entries()];
  }, [jogos]);

  const fasesDoFiltro = useMemo(() => {
    const nomes = new Map<string, string>();
    for (const j of jogos) if (!filtro.categoria || j.categoria.id === filtro.categoria) nomes.set(j.fase.id, `${j.categoria.nome} › ${j.fase.nome}`);
    return [...nomes.entries()];
  }, [jogos, filtro.categoria]);

  const filtrados = useMemo(() => {
    const q = filtro.busca.trim().toLowerCase();
    return jogos.filter(j =>
      (!filtro.categoria || j.categoria.id === filtro.categoria) &&
      (!filtro.fase || j.fase.id === filtro.fase) &&
      (!filtro.status || j.status === filtro.status) &&
      (!filtro.local || j.local?.id === filtro.local) &&
      (!filtro.semData || !j.data) &&
      (!q || `${j.casa.texto} ${j.visitante.texto}`.toLowerCase().includes(q)));
  }, [jogos, filtro]);

  const blocos = useMemo(() => {
    const datados = filtrados.filter(j => j.data);
    const dias = [...new Set(datados.map(j => j.data!))].sort();
    return {
      dias: dias.map(d => ({
        dia: d,
        jogos: datados.filter(j => j.data === d).sort((a, b) => (a.local?.nome ?? '').localeCompare(b.local?.nome ?? '') || (a.hora ?? '').localeCompare(b.hora ?? '') || a.numero - b.numero),
      })),
      semData: filtrados.filter(j => !j.data),
    };
  }, [filtrados]);

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !dados || !id) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;

  const comData = jogos.filter(j => j.data).length;
  const encerrados = jogos.filter(j => j.status === 'Encerrado').length;
  const alternar = (jogoId: string, marcado: boolean) =>
    setSelecionados(atual => { const novo = new Set(atual); if (marcado) novo.add(jogoId); else novo.delete(jogoId); return novo; });
  const selecionadosOrdenados = jogos.filter(j => selecionados.has(j.id)).sort((a, b) => a.numero - b.numero);

  return (
    <div className={base.page} style={{ maxWidth: 1150 }}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(`/temporadas/${id}`)}>‹ Voltar</button>
      </div>

      <h1 className={base.titulo}>Jogos — Temporada {dados.ano}</h1>
      <p className={base.sub}>
        A tabela do campeonato inteira, com todas as categorias, por <b>data</b> e <b>local</b>. Defina data, horário e local jogo a jogo ou <b>em lote</b>.
      </p>

      {!dados.tabelaGerada ? (
        <div className={`${base.banner} ${base.bannerAviso}`}>
          <span><b>A tabela de jogos ainda não foi gerada.</b> Encerre o cadastro de fases e monte a tabela na tela de fases.</span>
          <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => navigate(`/temporadas/${id}/fases`)}>Ir para as fases ›</button>
        </div>
      ) : (
        <>
          <div className={base.chips}>
            <span className={base.chip}>Jogos: <b>{jogos.length}</b></span>
            <span className={base.chip}>Com data: <b>{comData}</b></span>
            <span className={base.chip}>Sem data: <b>{jogos.length - comData}</b></span>
            <span className={base.chip}>Encerrados: <b>{encerrados}</b></span>
          </div>

          <div className={styles.filtros}>
            <label className={styles.f}>Categoria
              <select className={base.select} value={filtro.categoria} onChange={e => setFiltro({ ...filtro, categoria: e.target.value, fase: '' })}>
                <option value="">Todas</option>
                {dados.categorias.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
              </select>
            </label>
            <label className={styles.f}>Fase
              <select className={base.select} value={filtro.fase} onChange={e => setFiltro({ ...filtro, fase: e.target.value })}>
                <option value="">Todas</option>
                {fasesDoFiltro.map(([fid, nome]) => <option key={fid} value={fid}>{nome}</option>)}
              </select>
            </label>
            <label className={styles.f}>Status
              <select className={base.select} value={filtro.status} onChange={e => setFiltro({ ...filtro, status: e.target.value })}>
                <option value="">Todos</option>
                {(Object.keys(STATUS_ROTULO) as StatusJogo[]).map(s => <option key={s} value={s}>{STATUS_ROTULO[s]}</option>)}
              </select>
            </label>
            <label className={styles.f}>Local
              <select className={base.select} value={filtro.local} onChange={e => setFiltro({ ...filtro, local: e.target.value })}>
                <option value="">Todos</option>
                {locaisDosJogos.map(([lid, nome]) => <option key={lid} value={lid}>{nome}</option>)}
              </select>
            </label>
            <label className={`${styles.f} ${styles.cresce}`}>Equipe
              <input className={base.input} placeholder="Buscar equipe…" value={filtro.busca} onChange={e => setFiltro({ ...filtro, busca: e.target.value })} />
            </label>
            <label className={styles.check}><input type="checkbox" checked={filtro.semData} onChange={e => setFiltro({ ...filtro, semData: e.target.checked })} /> Só sem data</label>
            <span className={styles.seg}>
              <button className={vista === 'data' ? styles.segOn : ''} onClick={() => setVista('data')}>Por data</button>
              <button className={vista === 'numero' ? styles.segOn : ''} onClick={() => setVista('numero')}>Por número</button>
            </span>
          </div>

          {filtrados.length === 0 && <div className={base.vazio}>Nenhum jogo com esses filtros.</div>}

          {vista === 'numero' && filtrados.length > 0 && (
            <div className={styles.cartao}><table className={styles.tabela}><tbody>{filtrados.map(j => <Linha key={j.id} j={j} marcado={selecionados.has(j.id)} onMarcar={m => alternar(j.id, m)} onAgendar={() => setAgendando(j)} />)}</tbody></table></div>
          )}

          {vista === 'data' && (
            <>
              {blocos.dias.map(({ dia, jogos: doDia }) => (
                <div key={dia} className={styles.cartao}>
                  <div className={styles.dia}>{rotuloDia(dia)} <small>{doDia.length} jogo{doDia.length === 1 ? '' : 's'}</small></div>
                  <table className={styles.tabela}>
                    <tbody>
                      {doDia.map((j, i) => (
                        <FragmentoLocal key={j.id} mostrar={i === 0 || doDia[i - 1].local?.id !== j.local?.id} local={j.local}>
                          <Linha j={j} marcado={selecionados.has(j.id)} onMarcar={m => alternar(j.id, m)} onAgendar={() => setAgendando(j)} />
                        </FragmentoLocal>
                      ))}
                    </tbody>
                  </table>
                </div>
              ))}
              {blocos.semData.length > 0 && (
                <div className={styles.cartao}>
                  <div className={`${styles.dia} ${styles.semData}`}>Sem data definida <small>{blocos.semData.length} jogo{blocos.semData.length === 1 ? '' : 's'}</small></div>
                  <table className={styles.tabela}><tbody>{blocos.semData.map(j => <Linha key={j.id} j={j} marcado={selecionados.has(j.id)} onMarcar={m => alternar(j.id, m)} onAgendar={() => setAgendando(j)} />)}</tbody></table>
                </div>
              )}
            </>
          )}
        </>
      )}

      {selecionados.size > 0 && (
        <div className={styles.barra}>
          <span>{selecionados.size} jogo{selecionados.size === 1 ? '' : 's'} selecionado{selecionados.size === 1 ? '' : 's'}</span>
          <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => setLote(true)}>Agendar em lote…</button>
          <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => setSelecionados(new Set())}>Limpar seleção</button>
        </div>
      )}

      {agendando && (
        <ModalAgendar
          jogo={agendando}
          titulo={`${agendando.casa.texto} × ${agendando.visitante.texto} · ${agendando.categoria.nome} · ${agendando.fase.nome}`}
          associacaoId={dados.associacaoId}
          onFechar={() => setAgendando(null)}
          onSalvo={async () => { setAgendando(null); setAviso({ texto: 'Jogo agendado.', erro: false }); await carregar(); }}
        />
      )}

      {lote && (
        <ModalLote
          temporadaId={id}
          associacaoId={dados.associacaoId}
          selecionados={selecionadosOrdenados}
          todos={jogos}
          onFechar={() => setLote(false)}
          onSalvo={async n => { setLote(false); setSelecionados(new Set()); setAviso({ texto: `${n} jogos agendados.`, erro: false }); await carregar(); }}
        />
      )}

      {aviso && <div className={`${base.toast} ${aviso.erro ? base.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

function Linha({ j, marcado, onMarcar, onAgendar }: { j: JogoLista; marcado: boolean; onMarcar: (marcado: boolean) => void; onAgendar: () => void }) {
  const enc = j.status === 'Encerrado';
  const vencA = enc && (j.placarCasa ?? 0) > (j.placarVisitante ?? 0);
  const vencB = enc && (j.placarVisitante ?? 0) > (j.placarCasa ?? 0);
  const pode = j.status === 'Agendado';
  return (
    <tr>
      <td className={styles.sel}><input type="checkbox" disabled={!pode} checked={marcado} aria-label={`Selecionar jogo ${j.numero}`} onChange={e => onMarcar(e.target.checked)} /></td>
      <td className={styles.hora}>{horaCurta(j.hora) || '—'}</td>
      <td className={styles.num}><Link to={`/jogos/${j.id}`} title="Abrir o jogo">#{j.numero}</Link></td>
      <td>
        <div className={styles.par}>
          <span className={`${styles.a} ${j.casa.definida ? '' : styles.ref} ${vencA ? styles.venc : ''}`}>{j.casa.texto}</span>
          <span className={styles.placar}>{enc ? j.placarCasa : ''}</span>
          <span className={styles.x}>×</span>
          <span className={styles.placar}>{enc ? j.placarVisitante : ''}</span>
          <span className={`${styles.b} ${j.visitante.definida ? '' : styles.ref} ${vencB ? styles.venc : ''}`}>{j.visitante.texto}</span>
        </div>
      </td>
      <td>
        <span className={styles.tagCat}>{j.categoria.nome}</span>{' '}
        <span className={styles.tag}>{j.fase.nome} · {j.etiqueta}</span>
        {j.opcional && <span className={styles.opcional}> se necessário</span>}
      </td>
      <td><span className={`${base.status} ${STATUS_ESTILO[j.status]}`}>{STATUS_ROTULO[j.status]}</span></td>
      <td className={styles.acoes}><button className={`${base.btnSecondary} ${base.btnSm}`} disabled={!pode} onClick={onAgendar}>Agendar</button></td>
    </tr>
  );
}

/** Cabeçalho do local (quando muda dentro do dia) seguido da linha do jogo. */
function FragmentoLocal({ mostrar, local, children }: { mostrar: boolean; local: JogoLista['local']; children: React.ReactNode }) {
  return (
    <>
      {mostrar && <tr><td colSpan={7} className={styles.local}>{local ? rotuloLocal(local) : 'Local a definir'}</td></tr>}
      {children}
    </>
  );
}

function ModalLote({ temporadaId, associacaoId, selecionados, todos, onFechar, onSalvo }: {
  temporadaId: string; associacaoId: string; selecionados: JogoLista[]; todos: JogoLista[];
  onFechar: () => void; onSalvo: (qtd: number) => Promise<void>;
}) {
  const [data, setData] = useState('');
  const [hora, setHora] = useState('08:30');
  const [intervalo, setIntervalo] = useState('90');
  const [localId, setLocalId] = useState('');
  const [locais, setLocais] = useState<LocalItem[]>([]);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  // A ordem define os horários: começa pela numeração e pode ser mudada.
  const [ordenados, setOrdenados] = useState<JogoLista[]>(selecionados);

  function mover(i: number, d: -1 | 1) {
    setOrdenados(p => {
      const j = i + d;
      if (j < 0 || j >= p.length) return p;
      const n = [...p];
      [n[i], n[j]] = [n[j], n[i]];
      return n;
    });
  }
  const naOrdemDaNumeracao = ordenados.every((j, i) => j.id === selecionados[i].id);

  useEffect(() => { listarLocais(associacaoId).then(setLocais).catch(() => setErro('Não foi possível carregar os locais.')); }, [associacaoId]);

  const plano = useMemo(() => {
    const [h, m] = (hora || '00:00').split(':').map(Number);
    return ordenados.map((j, i) => ({ jogo: j, hora: minutosParaHora(h * 60 + m + i * Number(intervalo)) }));
  }, [ordenados, hora, intervalo]);

  const avisos = useMemo(() => {
    if (!dataValida(data, 2000, 2100)) return [];
    const dia = ddmmParaIso(data);
    const ids = new Set(selecionados.map(j => j.id));
    const contagem = new Map<string, { nome: string; n: number }>();
    const contar = (equipeId: string | null, nome: string) => {
      if (!equipeId) return; // referências ainda não resolvidas não entram
      contagem.set(equipeId, { nome, n: (contagem.get(equipeId)?.n ?? 0) + 1 });
    };
    for (const j of todos) if (j.data === dia && !ids.has(j.id)) { contar(j.casa.equipeId, j.casa.texto); contar(j.visitante.equipeId, j.visitante.texto); }
    for (const j of ordenados) { contar(j.casa.equipeId, j.casa.texto); contar(j.visitante.equipeId, j.visitante.texto); }
    const lista = [...contagem.values()].filter(c => c.n > 1).map(c => `${c.nome} jogaria mais de uma vez no dia`);
    if (localId) {
      for (const j of todos) if (j.data === dia && j.local?.id === localId && !ids.has(j.id) && plano.some(p => p.hora === horaCurta(j.hora)))
        lista.push(`Já existe jogo às ${horaCurta(j.hora)} neste local (#${j.numero})`);
    }
    return [...new Set(lista)];
  }, [data, localId, selecionados, ordenados, todos, plano]);

  async function aplicar() {
    setErro('');
    if (!dataValida(data, 2000, 2100)) return setErro('Informe uma data válida.');
    if (!hora) return setErro('Informe o horário do primeiro jogo.');
    setSalvando(true);
    try {
      await agendarEmLote(temporadaId, {
        jogoIds: ordenados.map(j => j.id), data: ddmmParaIso(data), horaInicial: hora,
        intervaloMinutos: Number(intervalo), localId: localId || null,
      });
      await onSalvo(ordenados.length);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível agendar.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={base.modal}>
        <h2 className={base.modalTitulo}>Agendar em lote</h2>
        <p className={base.modalTexto}>{selecionados.length} jogo(s) selecionado(s); o 1º da lista começa no horário informado e os demais seguem o intervalo. Use ↑ ↓ para mudar a ordem.</p>
        <div className={base.linha2}>
          <div className={base.field}><label htmlFor="lt-data">Data</label><DataInput id="lt-data" value={data} onChange={setData} anoMin={2000} anoMax={2100} /></div>
          <div className={base.field}><label htmlFor="lt-hora">Horário do 1º jogo</label><input id="lt-hora" type="time" className={base.input} value={hora} onChange={e => setHora(e.target.value)} /></div>
        </div>
        <div className={base.linha2}>
          <div className={base.field}>
            <label htmlFor="lt-int">Intervalo entre jogos</label>
            <select id="lt-int" className={base.select} value={intervalo} onChange={e => setIntervalo(e.target.value)}>
              {[30, 45, 60, 75, 90, 120].map(m => <option key={m} value={m}>{m} min</option>)}
            </select>
          </div>
          <div className={base.field}>
            <label htmlFor="lt-local">Local</label>
            <select id="lt-local" className={base.select} value={localId} onChange={e => setLocalId(e.target.value)}>
              <option value="">— sem local —</option>
              {locais.map(l => <option key={l.id} value={l.id}>{rotuloLocal(l)}</option>)}
            </select>
          </div>
        </div>
        <div className={base.previa}>
          {plano.map((p, i) => (
            <div key={p.jogo.id} className={styles.previaLinha}>
              <span><b>{p.hora}</b> · #{p.jogo.numero} {p.jogo.casa.texto} × {p.jogo.visitante.texto} ({p.jogo.categoria.nome})</span>
              <span className={styles.previaOrdem}>
                <button type="button" className={`${base.btnSecondary} ${base.btnSm}`} disabled={i === 0 || salvando} onClick={() => mover(i, -1)} aria-label={`Subir o jogo ${p.jogo.numero}`}>↑</button>
                <button type="button" className={`${base.btnSecondary} ${base.btnSm}`} disabled={i === plano.length - 1 || salvando} onClick={() => mover(i, 1)} aria-label={`Descer o jogo ${p.jogo.numero}`}>↓</button>
              </span>
            </div>
          ))}
        </div>
        {!naOrdemDaNumeracao && (
          <button type="button" className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => setOrdenados(selecionados)}>Voltar à ordem da numeração</button>
        )}
        {avisos.length > 0 && <div className={styles.avisoLote}><b>Atenção:</b>{avisos.map(a => <div key={a}>{a}</div>)}</div>}
        {erro && <p className={base.fieldErro}>{erro}</p>}
        <div className={base.modalFooter}>
          <button type="button" className={base.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button type="button" className={base.btnPrimary} onClick={aplicar} disabled={salvando}>{salvando ? 'Aplicando...' : 'Aplicar'}</button>
        </div>
      </div>
    </div>
  );
}

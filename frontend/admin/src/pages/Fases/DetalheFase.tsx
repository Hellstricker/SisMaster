import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { DistribuicaoEquipes, StatusFase, TipoFase } from '../../api/fases';
import {
  type ConfrontoDaFase,
  type DetalheFase as Detalhe,
  type EquipeResumo,
  type FaseAnteriorOpcao,
  type JogoDaFase,
  type LocalItem,
  type Origem,
  type StatusJogo,
  type TipoReferencia,
  agendarJogo,
  criarLocal,
  definirConfronto,
  definirDistribuicaoManual,
  listarLocais,
  obterDetalheFase,
} from '../../api/jogos';
import DataInput, { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import base from './Fases.module.css';
import styles from './DetalheFase.module.css';

const TIPO_ROTULO: Record<TipoFase, string> = { PontosCorridos: 'Pontos corridos', Grupos: 'Grupos', MataMata: 'Mata-mata' };
const DISTRIBUICAO_ROTULO: Record<DistribuicaoEquipes, string> = { Serpentina: 'serpentina', Alternada: 'alternada', Manual: 'manual' };
const STATUS_FASE_ROTULO: Record<StatusFase, string> = { Planejada: 'Planejada', EmAndamento: 'Em andamento', Encerrada: 'Encerrada' };
const STATUS_FASE_ESTILO: Record<StatusFase, string> = { Planejada: base.stPlanejada, EmAndamento: base.stEmAndamento, Encerrada: base.stEncerrada };
const STATUS_JOGO_ROTULO: Record<StatusJogo, string> = {
  Agendado: 'Agendado', EmAndamento: 'Em andamento', Encerrado: 'Encerrado', WO: 'W.O.', Dispensado: 'Dispensado',
};
const STATUS_JOGO_ESTILO: Record<StatusJogo, string> = {
  Agendado: base.stPlanejada, EmAndamento: base.stEmAndamento, Encerrado: base.stEncerrada, WO: base.stEncerrada, Dispensado: base.stPlanejada,
};

function dataBr(iso: string | null): string {
  if (!iso) return '';
  const [y, m, d] = iso.split('-');
  return `${d}/${m}/${y}`;
}

const horaCurta = (hora: string | null) => (hora ? hora.slice(0, 5) : '');

function quando(j: JogoDaFase): string {
  if (!j.data) return 'a definir';
  const partes = [dataBr(j.data)];
  if (j.hora) partes.push(horaCurta(j.hora));
  if (j.local) partes.push(`${j.local.nome} — ${j.local.cidade}${j.local.estado ? `/${j.local.estado}` : ''}`);
  return partes.join(' · ');
}

function textoClassificacao(d: Detalhe): string {
  if (d.tipo === 'MataMata') return 'avançam os vencedores';
  if (d.classificadosPrimeiros == null) return 'classificam todos';
  const por = d.tipo === 'Grupos' ? ' de cada grupo' : '';
  const base = d.classificadosPrimeiros === 1 ? `classifica o 1º colocado${por}` : `classificam os ${d.classificadosPrimeiros} primeiros${por}`;
  return d.melhoresExtras > 0 ? `${base} + ${d.melhoresExtras} melhor(es) ${d.classificadosPrimeiros + 1}º colocado(s)` : base;
}

function resumo(d: Detalhe): string {
  if (d.tipo === 'MataMata') return `${d.numeroConfrontos} confronto(s) · melhor de ${d.jogosPorConfronto} · ${textoClassificacao(d)}`;
  const turnos = `${d.numeroTurnos} turno${d.numeroTurnos === 1 ? '' : 's'}`;
  if (d.tipo === 'Grupos') return `${d.numeroGrupos} grupos${d.distribuicao ? ` (${DISTRIBUICAO_ROTULO[d.distribuicao]})` : ''} · ${turnos} · ${textoClassificacao(d)}`;
  return `${turnos} · ${textoClassificacao(d)}`;
}

type Modal =
  | { tipo: 'agendar'; jogo: JogoDaFase; titulo: string }
  | { tipo: 'confronto'; numero: number; confronto: ConfrontoDaFase | null };

/**
 * Detalhe da fase: equipes/vagas, grupos, confrontos e jogos. Antes de a tabela de jogos ser gerada mostra uma
 * simulação (com os cruzamentos editáveis); depois, o que está gravado (só data, hora e local mudam).
 */
export default function DetalheFase() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<Detalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [modal, setModal] = useState<Modal | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      setDados(await obterDetalheFase(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar a fase.');
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

  const jogosPorRodada = useMemo(() => {
    const mapa = new Map<number, JogoDaFase[]>();
    for (const j of dados?.jogos ?? []) mapa.set(j.rodada, [...(mapa.get(j.rodada) ?? []), j]);
    return [...mapa.entries()].sort((a, b) => a[0] - b[0]);
  }, [dados]);

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !dados) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Fase não encontrada.'}</p></div>;

  const gerada = dados.temporada.tabelaGerada;
  const abrirAgendar = (jogo: JogoDaFase, titulo: string) => { if (gerada && jogo.id) setModal({ tipo: 'agendar', jogo, titulo }); };

  return (
    <div className={base.page}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(`/temporadas/${dados.temporada.id}/fases`)}>‹ Voltar às fases</button>
        {gerada && <button className={base.btnSecondary} style={{ marginLeft: 8 }} onClick={() => navigate(`/fases/${dados.id}/classificacao`)}>Classificação ›</button>}
      </div>

      <h1 className={base.titulo}>{dados.nome} — {dados.categoria.nome}</h1>
      <p className={base.sub}>
        {TIPO_ROTULO[dados.tipo]} · {resumo(dados)}{dados.faseAnteriorNome ? ` · vem de “${dados.faseAnteriorNome}”` : ''}
      </p>

      <div className={`${base.banner} ${gerada ? base.bannerTrava : base.bannerAberto}`}>
        {gerada ? (
          <span><b>Tabela de jogos gerada.</b> A estrutura está travada; só data, hora e local dos jogos podem mudar.</span>
        ) : (
          <span>
            <b>Prévia — tabela de jogos ainda não gerada.</b> O que você vê é uma simulação.
            {dados.temporada.cadastroFasesEncerrado ? ' Para gerar, use “Montar tabela de jogos” na tela de fases.' : ' Encerre o cadastro de fases para poder gerar a tabela.'}
          </span>
        )}
        <span className={`${base.status} ${STATUS_FASE_ESTILO[dados.status]}`} title="O status acompanha os jogos da fase">{STATUS_FASE_ROTULO[dados.status]}</span>
      </div>

      {dados.previa && dados.previaErro && (
        <div className={`${base.banner} ${base.bannerAviso}`}><span><b>Não foi possível simular:</b> {dados.previaErro}</span></div>
      )}

      <div className={base.chips}>
        <span className={base.chip}>Tipo: <b>{TIPO_ROTULO[dados.tipo]}</b></span>
        <span className={base.chip}>Equipes da categoria: <b>{dados.equipesDaCategoria.length}</b></span>
        <span className={base.chip}>{gerada ? 'Jogos' : 'Jogos previstos'}: <b>{dados.totalJogos}</b></span>
      </div>

      {dados.tipo === 'PontosCorridos' && (
        <section className={styles.secao}>
          <h2 className={styles.secaoTitulo}>Equipes da fase</h2>
          <div className={styles.vagas}>
            {dados.vagas.map(v => (
              <div key={v.id} className={styles.vaga}>
                <span className={styles.pos}>{v.posicao}</span>
                <NomeEquipe equipe={v.equipe} texto={v.texto} />
              </div>
            ))}
            {dados.vagas.length === 0 && <p className={base.info}>Sem equipes para mostrar.</p>}
          </div>
        </section>
      )}

      {dados.tipo === 'Grupos' && (
        <section className={styles.secao}>
          <h2 className={styles.secaoTitulo}>Grupos</h2>
          <div className={styles.grupos}>
            {dados.grupos.map(g => (
              <div key={g.ordem} className={styles.grupo}>
                <h3 className={styles.grupoNome}>Grupo {g.nome}</h3>
                {g.equipes.map(v => (
                  <div key={v.id} className={styles.vaga}>
                    <span className={styles.pos}>{v.posicao}º</span>
                    <NomeEquipe equipe={v.equipe} texto={v.texto} />
                  </div>
                ))}
              </div>
            ))}
            {dados.grupos.length === 0 && <p className={base.info}>Sem grupos para mostrar.</p>}
          </div>
          {dados.distribuicao === 'Manual' && dados.permiteEditarCruzamentos && (
            <EditorDeGrupos
              key={dados.vagas.map(v => `${v.id}:${v.grupoOrdem}`).join('|')}
              dados={dados}
              onSalvar={async grupos => {
                await definirDistribuicaoManual(dados.id, grupos);
                setAviso({ texto: 'Distribuição dos grupos salva.', erro: false });
                await carregar();
              }}
            />
          )}
          <p className={base.dica}>
            As equipes entram nos grupos pela posição na classificação da fase anterior (ou pela ordem das equipes, se for a primeira fase).
            Quando a fase de origem terminar, as equipes reais aparecem no lugar das referências.
          </p>
        </section>
      )}

      {dados.tipo === 'MataMata' && (
        <section className={styles.secao}>
          <h2 className={styles.secaoTitulo}>Confrontos <small>melhor de {dados.jogosPorConfronto}</small></h2>
          <p className={base.dica}>As equipes de cada confronto são resolvidas automaticamente quando a fase ou o confronto de origem termina; até lá aparece a origem.</p>
          {[...dados.confrontos.map(c => ({ numero: c.numero, confronto: c as ConfrontoDaFase | null })),
            ...dados.confrontosPendentes.map(n => ({ numero: n, confronto: null }))]
            .sort((a, b) => a.numero - b.numero)
            .map(({ numero, confronto }) => (
              <div key={numero} className={styles.confronto}>
                <div className={styles.confrontoTopo}>
                  <b>{confronto?.nome ?? `Confronto ${numero}`}</b>
                  {!confronto && <span className={base.incompleta}>cruzamento não definido</span>}
                  {dados.permiteEditarCruzamentos && (
                    <button className={`${base.btnSecondary} ${base.btnSm} ${styles.aDireita}`} onClick={() => setModal({ tipo: 'confronto', numero, confronto })}>
                      {confronto ? 'Editar cruzamento' : 'Definir cruzamento'}
                    </button>
                  )}
                </div>
                {confronto && (
                  <div className={styles.versus}>
                    <span className={styles.origem}>{confronto.origemA.texto}</span>
                    <span className={styles.x}>×</span>
                    <span className={styles.origem}>{confronto.origemB.texto}</span>
                  </div>
                )}
                {confronto && (
                  <div className={styles.serie}>
                    {confronto.jogos.map(j => (
                      <div key={`${j.jogoDaSerie}`} className={`${styles.jogoSerie} ${j.opcional ? styles.opcional : ''}`}>
                        <b>Jogo {j.jogoDaSerie}{j.opcional ? ' (se necessário)' : ''}</b>
                        <span>{gerada ? `#${j.numero} · ` : ''}{gerada ? quando(j) : `${j.casa.texto} × ${j.visitante.texto}`}</span>
                        {gerada && j.id && (
                          <>
                            <span className={`${base.status} ${STATUS_JOGO_ESTILO[j.status]}`}>{STATUS_JOGO_ROTULO[j.status]}</span>
                            <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => abrirAgendar(j, `${confronto.nome} · jogo ${j.jogoDaSerie}`)}>Agendar</button>
                          </>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
        </section>
      )}

      {dados.tipo !== 'MataMata' && (
        <section className={styles.secao}>
          <h2 className={styles.secaoTitulo}>Jogos</h2>
          {jogosPorRodada.length === 0 ? (
            <div className={base.vazio}>{dados.previaErro ? 'Sem prévia disponível.' : 'Nenhum jogo.'}</div>
          ) : (
            jogosPorRodada.map(([rodada, jogos], i) => (
              <details key={rodada} className={styles.rodada} open={i < 2}>
                <summary>Rodada {rodada} <span className={base.dica}>· {jogos.length} jogo{jogos.length === 1 ? '' : 's'}</span></summary>
                <table className={styles.tabela}>
                  <tbody>
                    {jogos.map(j => (
                      <tr key={`${j.grupo}-${j.casa.posicao}-${j.visitante.posicao}`}>
                        <td className={styles.num}>{j.numero != null ? `#${j.numero}` : '—'}</td>
                        <td>{j.grupo && <span className={styles.tagGrupo}>Grupo {j.grupo}</span>}</td>
                        <td className={styles.confrontoCel}>
                          <span className={styles.casa}>{j.casa.texto}</span><span className={styles.x}>×</span><span>{j.visitante.texto}</span>
                        </td>
                        <td className={styles.quando}>{gerada ? quando(j) : ''}</td>
                        <td>{gerada && <span className={`${base.status} ${STATUS_JOGO_ESTILO[j.status]}`}>{STATUS_JOGO_ROTULO[j.status]}</span>}</td>
                        <td>{gerada && j.id && <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => abrirAgendar(j, `${j.casa.texto} × ${j.visitante.texto}`)}>Agendar</button>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </details>
            ))
          )}
        </section>
      )}

      {modal?.tipo === 'agendar' && (
        <ModalAgendar
          jogo={modal.jogo}
          titulo={modal.titulo}
          associacaoId={dados.associacaoId}
          onFechar={() => setModal(null)}
          onSalvo={async () => { setModal(null); setAviso({ texto: 'Jogo agendado.', erro: false }); await carregar(); }}
        />
      )}

      {modal?.tipo === 'confronto' && (
        <ModalConfronto
          faseId={dados.id}
          numero={modal.numero}
          confronto={modal.confronto}
          fasesAnteriores={dados.fasesAnteriores}
          equipes={dados.equipesDaCategoria}
          onFechar={() => setModal(null)}
          onSalvo={async () => { setModal(null); setAviso({ texto: 'Cruzamento salvo.', erro: false }); await carregar(); }}
        />
      )}

      {aviso && <div className={`${base.toast} ${aviso.erro ? base.toastErro : ''}`}>{aviso.texto}</div>}
    </div>
  );
}

/** Distribuição manual: a diretoria escolhe o grupo de cada vaga (na ordem das vagas) até a tabela de jogos ser gerada. */
function EditorDeGrupos({ dados, onSalvar }: { dados: Detalhe; onSalvar: (grupos: number[]) => Promise<void> }) {
  const vagas = [...dados.vagas].sort((a, b) => a.posicao - b.posicao);
  const [grupos, setGrupos] = useState<number[]>(vagas.map(v => v.grupoOrdem ?? 1));
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const total = dados.numeroGrupos ?? 2;
  const nome = (ordem: number) => String.fromCharCode(64 + ordem);

  async function salvar() {
    for (let g = 1; g <= total; g++) {
      if (grupos.filter(x => x === g).length < 2) { setErro(`O grupo ${nome(g)} precisa de ao menos 2 equipes.`); return; }
    }
    setSalvando(true);
    try {
      await onSalvar(grupos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.grupo} style={{ marginTop: 12 }}>
      <h3 className={styles.grupoNome}>Escolher os grupos {dados.distribuicaoManualDefinida ? '' : '(ainda em blocos seguidos)'}</h3>
      {vagas.map((v, i) => (
        <div key={v.id} className={styles.vaga}>
          <span className={styles.pos}>{v.posicao}º</span>
          <NomeEquipe equipe={v.equipe} texto={v.texto} />
          <select
            className={base.select}
            style={{ marginLeft: 'auto', width: 'auto' }}
            value={grupos[i]}
            onChange={e => { setGrupos(p => p.map((g, j) => (j === i ? Number(e.target.value) : g))); setErro(''); }}
          >
            {Array.from({ length: total }, (_, k) => k + 1).map(g => <option key={g} value={g}>Grupo {nome(g)}</option>)}
          </select>
        </div>
      ))}
      {erro && <p className={base.erroGlobal}>{erro}</p>}
      <div style={{ marginTop: 8 }}>
        <button className={base.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar grupos'}</button>
      </div>
    </div>
  );
}

function NomeEquipe({ equipe, texto }: { equipe: EquipeResumo | null; texto: string }) {
  return (
    <span className={equipe ? styles.equipe : styles.referencia}>
      {equipe?.cor && <i className={styles.cor} style={{ background: equipe.cor }} />}
      {texto}
    </span>
  );
}

function Casca({ titulo, erro, salvando, onConfirmar, onFechar, children }: {
  titulo: string; erro: string; salvando: boolean; onConfirmar: () => void; onFechar: () => void; children: ReactNode;
}) {
  return (
    <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={base.modal}>
        <h2 className={base.modalTitulo}>{titulo}</h2>
        {children}
        {erro && <p className={base.fieldErro}>{erro}</p>}
        <div className={base.modalFooter}>
          <button type="button" className={base.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button type="button" className={base.btnPrimary} onClick={onConfirmar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </div>
    </div>
  );
}

/** O mínimo que o modal precisa saber do jogo (serve para a tela da fase e para a lista de jogos). */
export interface JogoAgendavel {
  id: string | null;
  numero: number | null;
  data: string | null;
  hora: string | null;
  local: LocalItem | null;
}

const NOVO_LOCAL = '__novo';

export function ModalAgendar({ jogo, titulo, associacaoId, onFechar, onSalvo }: {
  jogo: JogoAgendavel; titulo: string; associacaoId: string; onFechar: () => void; onSalvo: () => Promise<void>;
}) {
  const [data, setData] = useState(dataBr(jogo.data));
  const [hora, setHora] = useState(horaCurta(jogo.hora));
  const [localId, setLocalId] = useState(jogo.local?.id ?? '');
  const [locais, setLocais] = useState<LocalItem[]>([]);
  const [novo, setNovo] = useState({ nome: '', cidade: '', estado: '' });
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  useEffect(() => { listarLocais(associacaoId).then(setLocais).catch(() => setErro('Não foi possível carregar os locais.')); }, [associacaoId]);

  async function salvar() {
    setErro('');
    if (data && !dataValida(data)) return setErro('Data inválida.');
    if (hora && !data) return setErro('Informe a data junto com a hora.');
    setSalvando(true);
    try {
      let local: string | null = localId || null;
      if (localId === NOVO_LOCAL) {
        if (!novo.nome.trim() || !novo.cidade.trim()) throw new Error('Informe o nome e a cidade do novo local.');
        const estado = novo.estado.trim() || null;
        await criarLocal(associacaoId, { nome: novo.nome.trim(), cidade: novo.cidade.trim(), estado });
        const atualizados = await listarLocais(associacaoId);
        local = atualizados.find(l => l.nome === novo.nome.trim() && l.cidade === novo.cidade.trim())?.id ?? null;
      }
      await agendarJogo(jogo.id!, { data: data ? ddmmParaIso(data) : null, hora: hora || null, localId: local });
      await onSalvo();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível agendar.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Casca titulo={`Agendar jogo #${jogo.numero}`} erro={erro} salvando={salvando} onConfirmar={salvar} onFechar={onFechar}>
      <p className={base.modalTexto}>{titulo}</p>
      <div className={base.linha2}>
        <div className={base.field}><label htmlFor="ag-data">Data</label><DataInput id="ag-data" value={data} onChange={setData} anoMin={2000} anoMax={2100} /></div>
        <div className={base.field}><label htmlFor="ag-hora">Hora</label><input id="ag-hora" type="time" className={base.input} value={hora} onChange={e => setHora(e.target.value)} /></div>
      </div>
      <div className={base.field}>
        <label htmlFor="ag-local">Local</label>
        <select id="ag-local" className={base.select} value={localId} onChange={e => setLocalId(e.target.value)}>
          <option value="">— sem local —</option>
          {locais.map(l => <option key={l.id} value={l.id}>{l.nome} — {l.cidade}{l.estado ? `/${l.estado}` : ''}</option>)}
          <option value={NOVO_LOCAL}>+ Novo local…</option>
        </select>
      </div>
      {localId === NOVO_LOCAL && (
        <div className={base.caixa}>
          <div className={base.field}><label htmlFor="nl-nome">Nome</label><input id="nl-nome" className={base.input} maxLength={100} value={novo.nome} onChange={e => setNovo({ ...novo, nome: e.target.value })} /></div>
          <div className={base.linha2}>
            <div className={base.field}><label htmlFor="nl-cidade">Cidade</label><input id="nl-cidade" className={base.input} maxLength={100} value={novo.cidade} onChange={e => setNovo({ ...novo, cidade: e.target.value })} /></div>
            <div className={base.field} style={{ maxWidth: 90 }}><label htmlFor="nl-uf">UF</label><input id="nl-uf" className={base.input} maxLength={2} value={novo.estado} onChange={e => setNovo({ ...novo, estado: e.target.value.toUpperCase() })} /></div>
          </div>
        </div>
      )}
      <p className={base.dica}>Locais são cadastrados uma vez e reaproveitados entre jogos (inclusive rodadas em outras cidades). Deixe em branco para limpar.</p>
    </Casca>
  );
}

interface OrigemForm {
  tipo: TipoReferencia;
  faseId: string;
  grupoOrdem: string;
  posicao: string;
  confrontoFaseId: string;
  confrontoId: string;
  equipeId: string;
}

function formDaOrigem(o: Origem | null, fases: FaseAnteriorOpcao[]): OrigemForm {
  const vazio: OrigemForm = { tipo: 'Colocacao', faseId: '', grupoOrdem: '', posicao: '1', confrontoFaseId: '', confrontoId: '', equipeId: '' };
  if (!o) return vazio;
  return {
    tipo: o.tipo,
    faseId: o.faseId ?? '',
    grupoOrdem: o.grupoOrdem != null ? String(o.grupoOrdem) : '',
    posicao: o.posicao != null ? String(o.posicao) : '1',
    confrontoFaseId: o.confrontoId ? fases.find(f => f.confrontos.some(c => c.id === o.confrontoId))?.id ?? '' : '',
    confrontoId: o.confrontoId ?? '',
    equipeId: o.equipeId ?? '',
  };
}

function origemDoForm(f: OrigemForm, fases: FaseAnteriorOpcao[], rotulo: string): Omit<Origem, 'texto'> {
  const nulo = { equipeId: null, faseId: null, grupoOrdem: null, posicao: null, confrontoId: null };
  switch (f.tipo) {
    case 'Equipe':
      if (!f.equipeId) throw new Error(`${rotulo}: escolha a equipe.`);
      return { ...nulo, tipo: 'Equipe', equipeId: f.equipeId };
    case 'Colocacao': {
      const fase = fases.find(x => x.id === f.faseId);
      if (!fase) throw new Error(`${rotulo}: escolha a fase da colocação.`);
      const posicao = Number(f.posicao);
      if (!Number.isInteger(posicao) || posicao < 1) throw new Error(`${rotulo}: informe a posição (1 ou mais).`);
      if (fase.tipo === 'Grupos' && !f.grupoOrdem) throw new Error(`${rotulo}: escolha o grupo.`);
      return { ...nulo, tipo: 'Colocacao', faseId: fase.id, grupoOrdem: fase.tipo === 'Grupos' ? Number(f.grupoOrdem) : null, posicao };
    }
    default:
      if (!f.confrontoId) throw new Error(`${rotulo}: escolha o confronto.`);
      return { ...nulo, tipo: f.tipo, confrontoId: f.confrontoId };
  }
}

function EditorOrigem({ titulo, valor, onChange, fases, equipes }: {
  titulo: string; valor: OrigemForm; onChange: (v: OrigemForm) => void; fases: FaseAnteriorOpcao[]; equipes: EquipeResumo[];
}) {
  const fasesColocacao = fases.filter(f => f.tipo !== 'MataMata');
  const fasesConfronto = fases.filter(f => f.tipo === 'MataMata' && f.confrontos.length > 0);
  const faseColocacao = fasesColocacao.find(f => f.id === valor.faseId);
  const faseConfronto = fasesConfronto.find(f => f.id === valor.confrontoFaseId);
  const set = (parcial: Partial<OrigemForm>) => onChange({ ...valor, ...parcial });

  return (
    <fieldset className={base.caixa}>
      <legend>{titulo}</legend>
      <div className={base.field}>
        <label>Origem</label>
        <select className={base.select} value={valor.tipo} onChange={e => set({ tipo: e.target.value as TipoReferencia })}>
          <option value="Colocacao">Colocação em uma fase</option>
          <option value="Vencedor">Vencedor de um confronto</option>
          <option value="Perdedor">Perdedor de um confronto (ex.: disputa de 3º lugar)</option>
          <option value="Equipe">Equipe fixa</option>
        </select>
      </div>

      {valor.tipo === 'Colocacao' && (
        <div className={base.linha2}>
          <div className={base.field}>
            <label>Fase</label>
            <select className={base.select} value={valor.faseId} onChange={e => set({ faseId: e.target.value, grupoOrdem: '' })}>
              <option value="">Escolha…</option>
              {fasesColocacao.map(f => <option key={f.id} value={f.id}>{f.nome}</option>)}
            </select>
          </div>
          {faseColocacao?.tipo === 'Grupos' && (
            <div className={base.field}>
              <label>Grupo</label>
              <select className={base.select} value={valor.grupoOrdem} onChange={e => set({ grupoOrdem: e.target.value })}>
                <option value="">Escolha…</option>
                {Array.from({ length: faseColocacao.numeroGrupos ?? 0 }, (_, i) => <option key={i} value={i + 1}>Grupo {String.fromCharCode(65 + i)}</option>)}
              </select>
            </div>
          )}
          <div className={base.field} style={{ maxWidth: 110 }}>
            <label>Posição</label>
            <input type="number" min={1} className={base.input} value={valor.posicao} onChange={e => set({ posicao: e.target.value })} />
          </div>
        </div>
      )}

      {(valor.tipo === 'Vencedor' || valor.tipo === 'Perdedor') && (
        <div className={base.linha2}>
          <div className={base.field}>
            <label>Fase</label>
            <select className={base.select} value={valor.confrontoFaseId} onChange={e => set({ confrontoFaseId: e.target.value, confrontoId: '' })}>
              <option value="">Escolha…</option>
              {fasesConfronto.map(f => <option key={f.id} value={f.id}>{f.nome}</option>)}
            </select>
          </div>
          <div className={base.field}>
            <label>Confronto</label>
            <select className={base.select} value={valor.confrontoId} onChange={e => set({ confrontoId: e.target.value })} disabled={!faseConfronto}>
              <option value="">Escolha…</option>
              {faseConfronto?.confrontos.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
        </div>
      )}
      {(valor.tipo === 'Vencedor' || valor.tipo === 'Perdedor') && fasesConfronto.length === 0 && (
        <p className={base.dica}>Nenhuma fase de mata-mata anterior com confrontos definidos.</p>
      )}

      {valor.tipo === 'Equipe' && (
        <div className={base.field}>
          <label>Equipe</label>
          <select className={base.select} value={valor.equipeId} onChange={e => set({ equipeId: e.target.value })}>
            <option value="">Escolha…</option>
            {equipes.map(e => <option key={e.id} value={e.id}>{e.nome}</option>)}
          </select>
        </div>
      )}
    </fieldset>
  );
}

function ModalConfronto({ faseId, numero, confronto, fasesAnteriores, equipes, onFechar, onSalvo }: {
  faseId: string; numero: number; confronto: ConfrontoDaFase | null; fasesAnteriores: FaseAnteriorOpcao[]; equipes: EquipeResumo[];
  onFechar: () => void; onSalvo: () => Promise<void>;
}) {
  const [nome, setNome] = useState(confronto && confronto.nome !== `Confronto ${numero}` ? confronto.nome : '');
  const [a, setA] = useState(() => formDaOrigem(confronto?.origemA ?? null, fasesAnteriores));
  const [b, setB] = useState(() => formDaOrigem(confronto?.origemB ?? null, fasesAnteriores));
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function salvar() {
    setErro('');
    setSalvando(true);
    try {
      await definirConfronto(faseId, numero, {
        nome: nome.trim() || null,
        origemA: origemDoForm(a, fasesAnteriores, 'Primeira equipe'),
        origemB: origemDoForm(b, fasesAnteriores, 'Segunda equipe'),
      });
      await onSalvo();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar o cruzamento.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Casca titulo={`Cruzamento — confronto ${numero}`} erro={erro} salvando={salvando} onConfirmar={salvar} onFechar={onFechar}>
      <div className={base.field}>
        <label htmlFor="cf-nome">Nome (opcional)</label>
        <input id="cf-nome" className={base.input} maxLength={60} placeholder={`Confronto ${numero} (ex.: Final, 3º/4º lugar)`} value={nome} onChange={e => setNome(e.target.value)} />
      </div>
      <EditorOrigem titulo="Primeira equipe" valor={a} onChange={setA} fases={fasesAnteriores} equipes={equipes} />
      <EditorOrigem titulo="Segunda equipe" valor={b} onChange={setB} fases={fasesAnteriores} equipes={equipes} />
    </Casca>
  );
}

import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type DadosFase,
  type FaseItem,
  type FasesDaTemporada,
  type DistribuicaoEquipes,
  type StatusFase,
  type TipoFase,
  criarFase,
  distribuirEmGrupos,
  editarFase,
  encerrarCadastroFases,
  excluirFase,
  moverFase,
  obterFasesDaTemporada,
  reabrirCadastroFases,
} from '../../api/fases';
import { gerarTabelaJogos } from '../../api/jogos';
import { formatarData } from '../../utils/formatacao';
import styles from './Fases.module.css';

const TIPO_ROTULO: Record<TipoFase, string> = { PontosCorridos: 'Pontos corridos', Grupos: 'Grupos', MataMata: 'Mata-mata' };
const TIPO_DESCRICAO: Record<TipoFase, string> = {
  PontosCorridos: 'Todos contra todos',
  Grupos: 'Todos contra todos dentro de cada grupo',
  MataMata: 'Confrontos eliminatórios',
};
const DISTRIBUICAO_ROTULO: Record<DistribuicaoEquipes, string> = { Serpentina: 'serpentina', Alternada: 'alternada', Manual: 'manual' };

function textoClassificacao(f: FaseItem): string {
  if (f.classificadosPrimeiros == null) return 'classificam todos';
  const por = f.tipo === 'Grupos' ? ' de cada grupo' : '';
  const base = f.classificadosPrimeiros === 1 ? `classifica o 1º colocado${por}` : `classificam os ${f.classificadosPrimeiros} primeiros${por}`;
  if (f.melhoresExtras <= 0) return base;
  return `${base} + ${f.melhoresExtras} melhor${f.melhoresExtras > 1 ? 'es' : ''} ${f.classificadosPrimeiros + 1}º colocado${f.melhoresExtras > 1 ? 's' : ''}`;
}

/** Resume a estrutura da fase (o que a tabela de jogos vai usar). */
function resumoEstrutura(f: FaseItem): string {
  const turnos = f.numeroTurnos == null ? 'turnos a definir' : `${f.numeroTurnos} turno${f.numeroTurnos === 1 ? '' : 's'}`;
  if (f.tipo === 'MataMata') {
    const conf = f.numeroConfrontos == null ? 'confrontos a definir' : `${f.numeroConfrontos} confronto${f.numeroConfrontos === 1 ? '' : 's'}`;
    return `${conf} · ${f.jogosPorConfronto == null ? 'melhor de a definir' : `melhor de ${f.jogosPorConfronto}`} · avançam os vencedores`;
  }
  if (f.tipo === 'Grupos') {
    const grupos = f.numeroGrupos == null ? 'grupos a definir' : `${f.numeroGrupos} grupos${f.distribuicao ? ` (${DISTRIBUICAO_ROTULO[f.distribuicao]})` : ''}`;
    return `${grupos} · ${turnos} · ${textoClassificacao(f)}`;
  }
  return `${turnos} · ${textoClassificacao(f)}`;
}

const STATUS_ROTULO: Record<StatusFase, string> = { Planejada: 'Planejada', EmAndamento: 'Em andamento', Encerrada: 'Encerrada' };
const STATUS_ESTILO: Record<StatusFase, string> = { Planejada: styles.stPlanejada, EmAndamento: styles.stEmAndamento, Encerrada: styles.stEncerrada };

type Modal =
  | { tipo: 'form'; fase: FaseItem | null }
  | { tipo: 'excluir'; fase: FaseItem }
  | { tipo: 'encerrar' }
  | { tipo: 'gerar' };

/**
 * Fases da temporada: a sequência de fases de cada categoria (ordem automática, reordenável) e o encerramento do
 * cadastro — depois dele a tabela de jogos do campeonato é montada a partir das fases.
 */
export default function FasesTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<FasesDaTemporada | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [catId, setCatId] = useState<string | null>(null);
  const [modal, setModal] = useState<Modal | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const d = await obterFasesDaTemporada(id);
      setDados(d);
      setCatId(atual => (atual && d.categorias.some(c => c.temporadaCategoriaId === atual) ? atual : d.categorias[0]?.temporadaCategoriaId ?? null));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar as fases.');
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

  async function acao(fn: () => Promise<void>, sucesso: string) {
    try {
      await fn();
      setAviso({ texto: sucesso, erro: false });
      await carregar();
    } catch (e) {
      setAviso({ texto: e instanceof Error ? e.message : 'Operação não concluída.', erro: true });
    }
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !dados || !id) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;

  const cat = dados.categorias.find(c => c.temporadaCategoriaId === catId) ?? null;
  const statusPermite = dados.status === 'InscricoesEncerradas' || dados.status === 'EmAndamento';
  const travado = !dados.permiteCadastrar; // inscrições abertas, temporada encerrada ou cadastro encerrado
  const totalFases = dados.categorias.reduce((n, c) => n + c.fases.length, 0);

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/temporadas/${id}`)}>‹ Voltar</button>
      </div>

      <h1 className={styles.titulo}>Fases — Temporada {dados.ano}</h1>
      <p className={styles.sub}>
        Cada categoria tem a sua sequência de fases. A <b>ordem é automática</b> (1, 2, 3…) e pode ser reorganizada enquanto o cadastro
        está aberto. Quando o cadastro é <b>encerrado</b>, a tabela de jogos do campeonato é montada a partir dessas fases.
      </p>

      {!statusPermite ? (
        <div className={`${styles.banner} ${styles.bannerAviso}`}>
          <span>
            <b>Cadastro de fases indisponível.</b>{' '}
            {dados.status === 'Encerrada' ? 'A temporada está encerrada.' : 'Disponível depois de encerrar as inscrições da temporada.'}
          </span>
        </div>
      ) : dados.cadastroEncerrado ? (
        <div className={`${styles.banner} ${styles.bannerTrava}`}>
          <span>
            <b>Cadastro de fases encerrado{dados.cadastroFasesEncerradoEm ? ` em ${formatarData(dados.cadastroFasesEncerradoEm)}` : ''}.</b>{' '}
            {dados.tabelaJogosGerada ? 'A tabela de jogos já foi montada a partir das fases (definitivo).' : 'A tabela de jogos pode ser montada a partir das fases.'}
          </span>
          <span className={styles.bannerAcoes}>
            {!dados.tabelaJogosGerada && (
              <button className={`${styles.btnPrimary} ${styles.btnSm}`} disabled={!statusPermite}
                onClick={() => setModal({ tipo: 'gerar' })}>Montar tabela de jogos ›</button>
            )}
            <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={!dados.podeReabrir}
              title={dados.podeReabrir ? '' : 'Há jogos gerados: o encerramento é definitivo'}
              onClick={() => acao(() => reabrirCadastroFases(id), 'Cadastro de fases reaberto.')}>
              Reabrir cadastro
            </button>
          </span>
        </div>
      ) : (
        <div className={`${styles.banner} ${styles.bannerAberto}`}>
          <span><b>Cadastro de fases aberto.</b> Monte a sequência de cada categoria; ao terminar, encerre o cadastro para montar a tabela de jogos.</span>
          <button className={`${styles.btnPrimary} ${styles.btnSm}`} disabled={totalFases === 0} title={totalFases === 0 ? 'Cadastre ao menos uma fase' : ''}
            onClick={() => setModal({ tipo: 'encerrar' })}>
            Encerrar cadastro de fases
          </button>
        </div>
      )}

      <div className={styles.toolbar}>
        <div className={styles.tabs}>
          {dados.categorias.map(c => (
            <button key={c.temporadaCategoriaId} className={`${styles.tab} ${c.temporadaCategoriaId === catId ? styles.tabAtiva : ''}`}
              onClick={() => setCatId(c.temporadaCategoriaId)}>
              {c.nome}<small>{c.fases.length} fase{c.fases.length === 1 ? '' : 's'}</small>
            </button>
          ))}
        </div>
        <button className={styles.btnPrimary} disabled={travado || !cat} onClick={() => setModal({ tipo: 'form', fase: null })}>+ Nova fase</button>
      </div>

      {dados.categorias.length === 0 && <p className={styles.info}>A temporada não tem categorias.</p>}

      {cat && (
        <>
          <div className={styles.chips}>
            <span className={styles.chip}>Categoria: <b>{cat.nome}</b></span>
            <span className={styles.chip}>Equipes formadas: <b>{cat.equipes}</b></span>
          </div>

          {cat.fases.length === 0 ? (
            <div className={styles.vazio}>Nenhuma fase nesta categoria. Crie a primeira em “+ Nova fase”.</div>
          ) : (
            <div className={styles.fluxo}>
              {cat.fases.map((f, i) => (
                <div key={f.id} className={styles.fase}>
                  <div className={styles.rail}>
                    <div className={`${styles.num} ${f.status === 'EmAndamento' ? styles.numAndamento : f.status === 'Encerrada' ? styles.numEncerrada : ''}`}>{f.ordem}</div>
                    <div className={styles.linha} />
                  </div>
                  <div className={styles.card}>
                    <div className={styles.topo}>
                      <span className={styles.mover}>
                        <button disabled={travado || i === 0} title="Mover para cima" aria-label={`Mover ${f.nome} para cima`}
                          onClick={() => acao(() => moverFase(f.id, -1), `“${f.nome}” subiu uma posição.`)}>▲</button>
                        <button disabled={travado || i === cat.fases.length - 1} title="Mover para baixo" aria-label={`Mover ${f.nome} para baixo`}
                          onClick={() => acao(() => moverFase(f.id, 1), `“${f.nome}” desceu uma posição.`)}>▼</button>
                      </span>
                      <span className={styles.nome}>{f.nome}</span>
                      <span className={styles.tipo}>{TIPO_ROTULO[f.tipo]}</span>
                      <span className={`${styles.status} ${STATUS_ESTILO[f.status]}`} title="O status acompanha os jogos da fase">{STATUS_ROTULO[f.status]}</span>
                      <span className={styles.acoes}>
                        <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={!statusPermite} onClick={() => setModal({ tipo: 'form', fase: f })}>Editar</button>
                        <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} disabled={travado || f.temDependente}
                          title={f.temDependente ? 'Outra fase vem desta' : travado ? 'Cadastro travado' : ''}
                          onClick={() => setModal({ tipo: 'excluir', fase: f })}>Excluir</button>
                        <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => navigate(`/fases/${f.id}`)}>Detalhe ›</button>
                        <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => navigate(`/fases/${f.id}/classificacao`)}>Classificação ›</button>
                      </span>
                    </div>
                    <p className={styles.meta}>
                      {resumoEstrutura(f)}
                      {f.faseAnteriorNome ? ` · vem de “${f.faseAnteriorNome}”` : ''}
                    </p>
                    {!f.estruturaCompleta && (
                      <p className={styles.incompleta}>Estrutura incompleta: edite a fase para informar os dados que faltam antes de encerrar o cadastro.</p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {modal?.tipo === 'form' && cat && (
        <FormFase
          fase={modal.fase}
          fasesDaCategoria={cat.fases}
          equipes={cat.equipes}
          somenteNome={dados.cadastroEncerrado}
          onFechar={() => setModal(null)}
          onSalvar={async d => {
            if (modal.fase) await editarFase(modal.fase.id, d);
            else await criarFase(id, cat.temporadaCategoriaId, d);
            setModal(null);
            setAviso({ texto: modal.fase ? 'Fase atualizada.' : 'Fase criada no fim da sequência.', erro: false });
            await carregar();
          }}
        />
      )}

      {modal?.tipo === 'excluir' && (
        <Confirmar titulo="Excluir fase" confirmar="Excluir" perigo onFechar={() => setModal(null)}
          texto={`Excluir a fase “${modal.fase.nome}”? As fases seguintes serão renumeradas.`}
          onConfirmar={async () => { await excluirFase(modal.fase.id); setModal(null); setAviso({ texto: 'Fase excluída.', erro: false }); await carregar(); }} />
      )}

      {modal?.tipo === 'encerrar' && (
        <Confirmar titulo="Encerrar o cadastro de fases" confirmar="Encerrar cadastro" onFechar={() => setModal(null)}
          texto="A tabela de jogos do campeonato será montada com base nas fases cadastradas. Depois de encerrar não será possível criar, excluir ou reordenar fases, nem alterar tipo e parâmetros (só o nome). Você poderá reabrir enquanto a tabela de jogos não for gerada."
          onConfirmar={async () => { await encerrarCadastroFases(id); setModal(null); setAviso({ texto: 'Cadastro de fases encerrado.', erro: false }); await carregar(); }} />
      )}

      {modal?.tipo === 'gerar' && (
        <Confirmar titulo="Montar a tabela de jogos" confirmar="Montar tabela" onFechar={() => setModal(null)}
          texto="Serão gerados os jogos de todas as fases de todas as categorias da temporada, com numeração única. Os cruzamentos do mata-mata precisam estar definidos. Depois de gerada, a estrutura das fases e das equipes fica travada e não há como desfazer; data, hora e local dos jogos continuam editáveis."
          onConfirmar={async () => { await gerarTabelaJogos(id); setModal(null); setAviso({ texto: 'Tabela de jogos montada.', erro: false }); await carregar(); }} />
      )}

      {aviso && <div className={`${styles.toast} ${aviso.erro ? styles.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

const MELHOR_DE = [1, 3, 5, 7];

function FormFase({ fase, fasesDaCategoria, equipes, somenteNome, onSalvar, onFechar }: {
  fase: FaseItem | null;
  fasesDaCategoria: FaseItem[];
  equipes: number;
  somenteNome: boolean;
  onSalvar: (d: DadosFase) => Promise<void>;
  onFechar: () => void;
}) {
  const outras = fasesDaCategoria.filter(f => f.id !== fase?.id);
  const [nome, setNome] = useState(fase?.nome ?? '');
  const [tipo, setTipo] = useState<TipoFase>(fase?.tipo ?? 'PontosCorridos');
  const [turnos, setTurnos] = useState(String(fase?.numeroTurnos ?? 1));
  const [grupos, setGrupos] = useState(String(fase?.numeroGrupos ?? 2));
  const [distribuicao, setDistribuicao] = useState<DistribuicaoEquipes>(fase?.distribuicao ?? 'Serpentina');
  const [melhor, setMelhor] = useState(String(fase?.jogosPorConfronto ?? 3));
  const [confrontos, setConfrontos] = useState(String(fase?.numeroConfrontos ?? 1));
  const [soOsPrimeiros, setSoOsPrimeiros] = useState(fase?.classificadosPrimeiros != null);
  const [primeiros, setPrimeiros] = useState(String(fase?.classificadosPrimeiros ?? 1));
  const [extras, setExtras] = useState(String(fase?.melhoresExtras ?? 0));
  // Fase nova: sugere como anterior a última fase da sequência.
  const [anterior, setAnterior] = useState(fase ? fase.faseAnteriorId ?? '' : outras[outras.length - 1]?.id ?? '');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const g = Number(grupos);
  const porGrupo = tipo === 'Grupos' && Number.isInteger(g) && g >= 2 ? Math.floor(equipes / g) : equipes;
  const n = Number(primeiros) || 1;

  const previa = useMemo(() => {
    if (tipo !== 'Grupos' || distribuicao === 'Manual' || !Number.isInteger(g) || g < 2 || g > 8 || equipes === 0 || porGrupo < 2) return null;
    return distribuirEmGrupos(equipes, g, distribuicao);
  }, [tipo, distribuicao, g, equipes, porGrupo]);

  function validar(): string {
    if (!nome.trim()) return 'Informe o nome.';
    if (somenteNome) return '';
    if (tipo === 'Grupos') {
      if (!Number.isInteger(g) || g < 2 || g > 8) return 'O número de grupos deve estar entre 2 e 8.';
      if (equipes > 0 && porGrupo < 2) return `${equipes} equipes não formam ${g} grupos de pelo menos 2 equipes.`;
    }
    if (tipo === 'MataMata') {
      const c = Number(confrontos);
      if (!Number.isInteger(c) || c < 1 || c > 16) return 'O número de confrontos deve estar entre 1 e 16.';
    } else if (soOsPrimeiros) {
      const maximo = equipes > 0 ? porGrupo : 0;
      if (!Number.isInteger(n) || n < 1 || (maximo > 0 && n > maximo)) return `Quantos se classificam deve estar entre 1 e ${maximo || '…'}.`;
      const m = Number(extras) || 0;
      if (tipo === 'Grupos' && m > 0) {
        if (maximo > 0 && n >= maximo) return `Não existe o ${n + 1}º colocado em cada grupo para completar com os melhores.`;
        if (m >= g) return 'Os melhores colocados extras devem ser menos que o número de grupos.';
      }
    }
    return '';
  }

  async function salvar() {
    const msg = validar();
    if (msg) { setErro(msg); return; }
    setSalvando(true);
    try {
      await onSalvar({
        nome: nome.trim(),
        tipo,
        numeroTurnos: tipo === 'MataMata' ? null : Number(turnos),
        numeroGrupos: tipo === 'Grupos' ? g : null,
        distribuicao: tipo === 'Grupos' ? distribuicao : null,
        jogosPorConfronto: tipo === 'MataMata' ? Number(melhor) : null,
        numeroConfrontos: tipo === 'MataMata' ? Number(confrontos) : null,
        classificadosPrimeiros: tipo !== 'MataMata' && soOsPrimeiros ? n : null,
        melhoresExtras: tipo === 'Grupos' && soOsPrimeiros ? Number(extras) || 0 : 0,
        faseAnteriorId: anterior || null,
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
      setSalvando(false);
    }
  }

  const alterar = (setter: (v: string) => void) => (e: { target: { value: string } }) => { setter(e.target.value); setErro(''); };

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>{fase ? 'Editar fase' : 'Nova fase'}</h2>

        <div className={styles.field}>
          <label htmlFor="f-nome">Nome</label>
          <input id="f-nome" className={styles.input} maxLength={100} placeholder="Ex.: Fase de grupos" autoFocus value={nome} onChange={alterar(setNome)} />
        </div>

        <div className={styles.field}>
          <label>Tipo</label>
          <div className={styles.tipos}>
            {(Object.keys(TIPO_ROTULO) as TipoFase[]).map(tp => (
              <button key={tp} type="button" disabled={somenteNome} className={`${styles.tipoCard} ${tipo === tp ? styles.tipoSel : ''}`}
                onClick={() => { setTipo(tp); setErro(''); }}>
                <b>{TIPO_ROTULO[tp]}</b><span>{TIPO_DESCRICAO[tp]}</span>
              </button>
            ))}
          </div>
        </div>

        {tipo === 'MataMata' ? (
          <div className={styles.linha2}>
            <div className={styles.field}>
              <label htmlFor="f-conf">Número de confrontos</label>
              <input id="f-conf" className={styles.input} inputMode="numeric" disabled={somenteNome} value={confrontos}
                onChange={e => { setConfrontos(e.target.value.replace(/\D/g, '').slice(0, 2)); setErro(''); }} />
            </div>
            <div className={styles.field}>
              <label htmlFor="f-melhor">Melhor de</label>
              <select id="f-melhor" className={styles.select} disabled={somenteNome} value={melhor} onChange={alterar(setMelhor)}>
                {MELHOR_DE.map(m => <option key={m} value={m}>{m} jogo{m === 1 ? '' : 's'}</option>)}
              </select>
            </div>
          </div>
        ) : (
          <div className={styles.linha2}>
            {tipo === 'Grupos' && (
              <div className={styles.field}>
                <label htmlFor="f-grupos">Número de grupos</label>
                <input id="f-grupos" className={styles.input} inputMode="numeric" disabled={somenteNome} value={grupos}
                  onChange={e => { setGrupos(e.target.value.replace(/\D/g, '').slice(0, 1)); setErro(''); }} />
              </div>
            )}
            <div className={styles.field}>
              <label htmlFor="f-turnos">Turnos</label>
              <select id="f-turnos" className={styles.select} disabled={somenteNome} value={turnos} onChange={alterar(setTurnos)}>
                {[1, 2, 3].map(x => <option key={x} value={x}>{x} turno{x === 1 ? '' : 's'}</option>)}
              </select>
            </div>
          </div>
        )}

        {tipo === 'Grupos' && (
          <fieldset className={styles.caixa}>
            <legend>Como as equipes entram nos grupos</legend>
            <div className={styles.radios}>
              {(['Serpentina', 'Alternada', 'Manual'] as const).map(d => (
                <label key={d}>
                  <input type="radio" name="f-distrib" disabled={somenteNome} checked={distribuicao === d} onChange={() => { setDistribuicao(d); setErro(''); }} /> {d}
                </label>
              ))}
            </div>
            <p className={styles.dica}>Com base na classificação da fase anterior (ou na ordem das equipes, se for a primeira fase).</p>
            <div className={styles.previa}>
              {distribuicao === 'Manual' ? 'Distribuição manual: a diretoria escolhe a equipe de cada grupo no detalhe da fase.'
                : equipes === 0 ? 'Ainda não há equipes formadas nesta categoria para mostrar a prévia.'
                : previa ? (
                  <>
                    <b>Prévia com {equipes} equipes ({distribuicao.toLowerCase()}):</b>
                    <div>{previa.map((pos, i) => <span key={i} className={styles.previaGrupo}><b>Grupo {String.fromCharCode(65 + i)}:</b> {pos.map(p => `${p}º`).join(', ')}</span>)}</div>
                  </>
                ) : <b>{equipes} equipes não formam {grupos || '…'} grupos de pelo menos 2 equipes.</b>}
            </div>
          </fieldset>
        )}

        {tipo !== 'MataMata' && (
          <fieldset className={styles.caixa}>
            <legend>Quem se classifica para a próxima fase</legend>
            <div className={styles.radios}>
              <label><input type="radio" name="f-classif" disabled={somenteNome} checked={!soOsPrimeiros} onChange={() => { setSoOsPrimeiros(false); setErro(''); }} /> Todos</label>
              <label><input type="radio" name="f-classif" disabled={somenteNome} checked={soOsPrimeiros} onChange={() => { setSoOsPrimeiros(true); setErro(''); }} /> Os primeiros</label>
            </div>
            {soOsPrimeiros && (
              <div className={styles.linha2}>
                <div className={styles.field}>
                  <label htmlFor="f-n">{tipo === 'Grupos' ? 'Quantos de cada grupo' : 'Quantos (no total)'}</label>
                  <input id="f-n" className={styles.input} inputMode="numeric" disabled={somenteNome} value={primeiros}
                    onChange={e => { setPrimeiros(e.target.value.replace(/\D/g, '').slice(0, 2)); setErro(''); }} />
                </div>
                {tipo === 'Grupos' && (
                  <div className={styles.field}>
                    <label htmlFor="f-extras">+ melhores {n + 1}º colocados</label>
                    <input id="f-extras" className={styles.input} inputMode="numeric" disabled={somenteNome} value={extras}
                      onChange={e => { setExtras(e.target.value.replace(/\D/g, '').slice(0, 1)); setErro(''); }} />
                  </div>
                )}
              </div>
            )}
            {soOsPrimeiros && (
              <p className={styles.dica}>
                {tipo === 'Grupos'
                  ? `Ex.: 1 de cada grupo + 2 melhores 2º colocados.${equipes > 0 ? ` Cada grupo tem ${porGrupo} equipe(s).` : ''}`
                  : `Os N primeiros colocados avançam.${equipes > 0 ? ` A categoria tem ${equipes} equipes.` : ''}`}
              </p>
            )}
          </fieldset>
        )}

        <div className={styles.field}>
          <label htmlFor="f-ant">Fase anterior (de onde vêm os classificados)</label>
          <select id="f-ant" className={styles.select} disabled={somenteNome} value={anterior} onChange={alterar(setAnterior)}>
            <option value="">— nenhuma (primeira fase) —</option>
            {outras.map(f => <option key={f.id} value={f.id}>{f.ordem} · {f.nome}</option>)}
          </select>
        </div>

        <p className={styles.dica}>
          {somenteNome
            ? 'Cadastro encerrado: só o nome pode ser alterado.'
            : tipo === 'MataMata'
              ? 'Os cruzamentos (quem enfrenta quem) são montados no detalhe da fase. A posição na sequência é automática; reorganize com ▲ ▼.'
              : 'A posição na sequência é automática (a nova fase vai para o fim); reorganize depois com ▲ ▼. A fase anterior precisa estar antes desta na sequência.'}
        </p>
        {erro && <p className={styles.fieldErro}>{erro}</p>}

        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </div>
    </div>
  );
}

function Confirmar({ titulo, texto, confirmar, perigo, onConfirmar, onFechar }: {
  titulo: string;
  texto: string;
  confirmar: string;
  perigo?: boolean;
  onConfirmar: () => Promise<void>;
  onFechar: () => void;
}) {
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function executar() {
    setSalvando(true);
    try {
      await onConfirmar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Operação não concluída.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal} style={{ maxWidth: 460 }}>
        <h2 className={styles.modalTitulo}>{titulo}</h2>
        <p className={styles.modalTexto}>{texto}</p>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={`${styles.btnPrimary} ${perigo ? styles.btnPerigoSolido : ''}`} onClick={executar} disabled={salvando}>
            {salvando ? 'Aguarde...' : confirmar}
          </button>
        </div>
      </div>
    </div>
  );
}

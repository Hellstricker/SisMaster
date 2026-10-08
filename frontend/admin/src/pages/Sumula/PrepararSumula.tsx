import { useCallback, useEffect, useMemo, useState } from 'react';
import EncerrarSumula from './EncerrarSumula';
import { AREA_ARBITROS } from '../../utils/area';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type LadoTime,
  type PreparoSumula,
  type TimePreparo,
  acrescentarAtleta,
  iniciarSumula,
  linksDaSumula,
  obterPreparoSumula,
  prepararSumula,
  salvarRelacao,
} from '../../api/jogos';
import base from '../Fases/Fases.module.css';
import styles from './Sumula.module.css';

interface LinhaAtleta {
  atletaId: string;
  nome: string;
  selecionado: boolean;
  numero: string;
  titular: boolean;
  /** Período em que foi acrescentado depois do início do jogo (nulo = relacionado desde o começo). */
  chegouNoPeriodo: number | null;
}

interface FormTime {
  lado: LadoTime;
  nome: string;
  tecnico: string;
  auxiliar: string;
  capitaoAtletaId: string | null;
  atletas: LinhaAtleta[];
}

const CAMISA_VALIDA = /^(0|00|[1-9][0-9]?)$/;

function formDoTime(t: TimePreparo): FormTime {
  const porAtleta = new Map(t.jogadores.map(j => [j.atletaId, j]));
  return {
    lado: t.lado,
    nome: t.nome,
    tecnico: t.tecnico ?? '',
    auxiliar: t.auxiliarTecnico ?? '',
    capitaoAtletaId: t.capitaoAtletaId,
    atletas: t.elenco.map(a => {
      const j = porAtleta.get(a.atletaId);
      // A camisa começa vazia: nunca vem de outro jogo.
      return { atletaId: a.atletaId, nome: a.nome, selecionado: !!j, numero: j?.numero ?? '', titular: j?.titular ?? false, chegouNoPeriodo: j?.chegouNoPeriodo ?? null };
    }),
  };
}

/** Erro de camisa por atleta (índice → mensagem). */
function errosDeCamisa(f: FormTime): Record<number, string> {
  const erros: Record<number, string> = {};
  const usadas = new Map<string, number>();
  f.atletas.forEach((a, i) => {
    if (!a.selecionado) return;
    if (a.numero === '') { erros[i] = 'Informe a camisa'; return; }
    if (!CAMISA_VALIDA.test(a.numero)) { erros[i] = 'Camisa 0, 00 ou 1 a 99'; return; }
    const outro = usadas.get(a.numero);
    if (outro !== undefined) { erros[i] = `Camisa repetida (${a.numero})`; erros[outro] = `Camisa repetida (${a.numero})`; return; }
    usadas.set(a.numero, i);
  });
  return erros;
}

function pendenciasDe(f: FormTime, minimo: number, titulares: number): string[] {
  const sel = f.atletas.filter(a => a.selecionado);
  const p: string[] = [];
  if (sel.length < minimo) p.push(`mínimo de ${minimo} relacionados`);
  if (Object.keys(errosDeCamisa(f)).length > 0) p.push('corrigir as camisas');
  const tit = sel.filter(a => a.titular).length;
  // Com 5 ou mais relacionados, 5 titulares; com 4, todos começam em quadra.
  const esperados = Math.min(titulares, sel.length);
  if (sel.length >= minimo && tit !== esperados)
    p.push(esperados === titulares ? `marcar exatamente ${titulares} titulares (${tit})` : `marcar os ${esperados} relacionados como titulares (${tit})`);
  if (!f.capitaoAtletaId || !sel.some(a => a.atletaId === f.capitaoAtletaId)) p.push('definir o capitão');
  return p;
}

/**
 * Preparar a súmula do jogo: relação dos jogadores de cada equipe (do elenco), camisa do jogo, titulares, capitão e
 * comissão técnica. Tudo vazio por padrão — nada vem de outro jogo. Ao iniciar, a relação trava.
 */
export default function PrepararSumula() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<PreparoSumula | null>(null);
  const [forms, setForms] = useState<FormTime[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);
  // Camisa digitada para quem está sendo acrescentado depois do início do jogo.
  const [camisaNova, setCamisaNova] = useState<Record<string, string>>({});

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const d = await obterPreparoSumula(id);
      setDados(d);
      setForms(d.times.map(formDoTime));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar a súmula.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(null), 5000);
    return () => clearTimeout(t);
  }, [aviso]);

  const regras = dados?.regras;
  const pendencias = useMemo(
    () => (regras ? forms.map(f => pendenciasDe(f, regras.minimoParaIniciar, regras.titulares)) : []),
    [forms, regras],
  );

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !dados || !id || !regras) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Jogo não encontrado.'}</p></div>;

  const editavel = dados.sumula?.editavel ?? false;
  const podeAcrescentar = dados.sumula?.podeAcrescentarAtletas ?? false;
  const prontoParaIniciar = editavel && forms.length === 2 && pendencias.every(p => p.length === 0);

  const atualiza = (lado: LadoTime, mudar: (f: FormTime) => FormTime) =>
    setForms(atual => atual.map(f => (f.lado === lado ? mudar(f) : f)));

  async function salvarTudo() {
    for (const f of forms) {
      await salvarRelacao(id!, {
        lado: f.lado,
        tecnico: f.tecnico.trim() || null,
        auxiliarTecnico: f.auxiliar.trim() || null,
        capitaoAtletaId: f.capitaoAtletaId,
        jogadores: f.atletas.filter(a => a.selecionado).map(a => ({ atletaId: a.atletaId, numero: a.numero, titular: a.titular })),
      });
    }
  }

  async function acao(fn: () => Promise<void>, sucesso: string) {
    setSalvando(true);
    try {
      await fn();
      setAviso({ texto: sucesso, erro: false });
      await carregar();
    } catch (e) {
      setAviso({ texto: e instanceof Error ? e.message : 'Operação não concluída.', erro: true });
    } finally {
      setSalvando(false);
    }
  }

  const titulo = `Súmula — Jogo #${dados.jogo.numero} · ${dados.jogo.categoria} · ${dados.jogo.fase}`;

  return (
    <div className={base.page}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(AREA_ARBITROS ? '/' : `/jogos/${id}`)}>{AREA_ARBITROS ? '‹ Voltar aos jogos' : '‹ Voltar ao jogo'}</button>
        {/* Importar jogo já realizado: coisa da diretoria/administrador, não do mesário. */}
        {!AREA_ARBITROS && dados.existe && (
          <button className={`${base.btnSecondary} ${base.btnSm}`} style={{ float: 'right' }} onClick={() => navigate(`/jogos/${id}/sumula/importar-fiba`)}>
            Importar do FIBA LiveStats ›
          </button>
        )}
      </div>

      <h1 className={base.titulo}>{titulo}</h1>
      <p className={base.sub}>
        Relação dos jogadores de cada equipe, camisa do jogo, titulares (5; com apenas 4 relacionados, todos), capitão e, opcionalmente, a comissão técnica. Máximo de {regras.maximo} relacionados por equipe;
        a relação trava quando a súmula é iniciada.
      </p>

      {!dados.existe || !dados.sumula ? (
        <div className={`${base.banner} ${base.bannerAviso}`}>
          <span><b>Este jogo ainda não tem súmula.</b> Prepare-a para informar a relação dos jogadores.</span>
          <button className={`${base.btnPrimary} ${base.btnSm}`} disabled={salvando}
            onClick={() => acao(() => prepararSumula(id), 'Preparação da súmula iniciada.')}>Preparar súmula</button>
        </div>
      ) : !editavel ? (
        <div className={`${base.banner} ${base.bannerTrava}`}>
          <span><b>{dados.sumula.status === 'Encerrada' ? 'Súmula encerrada.' : 'Súmula em andamento.'}</b> A relação dos jogadores está travada.</span>
          <span className={base.bannerAcoes}>
            {(() => {
              const l = linksDaSumula(dados.sumula!.id);
              return (
                <>
                  {/* Só o mesário (área do árbitro) opera tempo e estatísticas; a gestão acompanha o placar. */}
                  {AREA_ARBITROS && (
                    <>
                      <a className={`${base.btnSecondary} ${base.btnSm} ${styles.link}`} href={l.mesarioTempo} target="_blank" rel="noreferrer">Mesário — tempo</a>
                      <a className={`${base.btnSecondary} ${base.btnSm} ${styles.link}`} href={l.mesarioStats} target="_blank" rel="noreferrer">Mesário — estatísticas</a>
                    </>
                  )}
                  <a className={`${base.btnSecondary} ${base.btnSm} ${styles.link}`} href={l.placar} target="_blank" rel="noreferrer">Placar</a>
                  {dados.sumula!.status !== 'Encerrada' && (
                    <EncerrarSumula pequeno sumulaId={dados.sumula!.id} jogo={titulo}
                      onEncerrada={async () => { if (AREA_ARBITROS) navigate('/'); else await carregar(); }} />
                  )}
                </>
              );
            })()}
          </span>
        </div>
      ) : prontoParaIniciar ? (
        <div className={`${base.banner} ${base.bannerAberto}`}>
          <span>
            <b>Tudo pronto para iniciar a súmula.</b>
            {dados.categoriaComRodizio && forms.some(f => f.atletas.filter(a => a.selecionado).length < regras.minimoParaRodizio) && (
              <><br />Atenção: equipe com menos de {regras.minimoParaRodizio} relacionados não cumpre o rodízio e perde os pontos da vitória.</>
            )}
          </span>
          <span className={base.bannerAcoes}>
            <button className={`${base.btnSecondary} ${base.btnSm}`} disabled={salvando} onClick={() => acao(salvarTudo, 'Relação salva.')}>Salvar</button>
            <button className={`${base.btnPrimary} ${base.btnSm}`} disabled={salvando}
              onClick={() => acao(async () => { await salvarTudo(); await iniciarSumula(id); }, 'Súmula iniciada.')}>Iniciar súmula ›</button>
          </span>
        </div>
      ) : (
        <div className={`${base.banner} ${base.bannerAviso}`}>
          <span>
            <b>Para iniciar a súmula:</b>
            {forms.map((f, i) => pendencias[i].length > 0 && <span key={f.lado}><br /><b>{f.nome}:</b> {pendencias[i].join(', ')}</span>)}
          </span>
          <span className={base.bannerAcoes}>
            <button className={`${base.btnSecondary} ${base.btnSm}`} disabled={salvando} onClick={() => acao(salvarTudo, 'Relação salva.')}>Salvar</button>
            <button className={`${base.btnPrimary} ${base.btnSm}`} disabled>Iniciar súmula ›</button>
          </span>
        </div>
      )}

      <div className={styles.colunas}>
        {dados.existe && forms.map((f, idx) => {
          const erros = errosDeCamisa(f);
          const sel = f.atletas.filter(a => a.selecionado);
          const titulares = sel.filter(a => a.titular).length;
          const cheio = sel.length >= regras.maximo;
          const abaixoRodizio = dados.categoriaComRodizio && sel.length >= regras.minimoParaIniciar && sel.length < regras.minimoParaRodizio;
          const nivel = sel.length < regras.minimoParaIniciar ? styles.contRuim : abaixoRodizio ? styles.contAviso : styles.contOk;
          const desabilitado = !editavel || salvando;
          return (
            <div key={f.lado} className={styles.time}>
              <h3 className={styles.timeNome}>
                {f.nome} <span className={styles.lado}>({f.lado === 'Casa' ? 'casa' : 'visitante'})</span>
              </h3>
              <div className={`${styles.contador} ${nivel}`}>
                {sel.length} de {regras.maximo} relacionados (máx.) · {editavel ? `${titulares} de ${Math.min(regras.titulares, Math.max(sel.length, regras.minimoParaIniciar))} titulares` : `${titulares} titulares`}
                {sel.length < regras.minimoParaIniciar ? ` · mínimo ${regras.minimoParaIniciar}` : ''}
                {abaixoRodizio ? ` · abaixo de ${regras.minimoParaRodizio} (rodízio)` : ''}
              </div>

              <div className={styles.comissao}>
                <div>
                  <label htmlFor={`tec-${f.lado}`}>Técnico <small>(opcional)</small></label>
                  <input id={`tec-${f.lado}`} className={base.input} maxLength={100} disabled={desabilitado} placeholder="Nome" value={f.tecnico}
                    onChange={e => atualiza(f.lado, x => ({ ...x, tecnico: e.target.value }))} />
                </div>
                <div>
                  <label htmlFor={`aux-${f.lado}`}>Auxiliar técnico <small>(opcional)</small></label>
                  <input id={`aux-${f.lado}`} className={base.input} maxLength={100} disabled={desabilitado} placeholder="Nome (opcional)" value={f.auxiliar}
                    onChange={e => atualiza(f.lado, x => ({ ...x, auxiliar: e.target.value }))} />
                </div>
              </div>

              {editavel && (
                <div className={styles.ferramentas}>
                  <button className={`${base.btnSecondary} ${base.btnSm}`} disabled={salvando}
                    onClick={() => atualiza(f.lado, x => ({ ...x, capitaoAtletaId: null, atletas: x.atletas.map(a => ({ ...a, selecionado: false, titular: false })) }))}>
                    Limpar seleção
                  </button>
                </div>
              )}

              <div className={`${styles.atleta} ${styles.cabecalho}`}>
                <span /><span>Atleta do elenco</span><span>Camisa</span><span title="Titular (os que começam em quadra)">Tit.</span><span title="Capitão">Cap.</span>
              </div>
              {f.atletas.length === 0 && <p className={base.info}>A equipe não tem atletas no elenco.</p>}
              {f.atletas.map((a, i) => (
                <div key={a.atletaId} className={`${styles.atleta} ${a.selecionado ? '' : styles.fora}`}>
                  <input type="checkbox" checked={a.selecionado} aria-label={`Relacionar ${a.nome}`}
                    disabled={desabilitado || (!a.selecionado && cheio)} title={!a.selecionado && cheio ? `Máximo de ${regras.maximo} relacionados` : ''}
                    onChange={e => atualiza(f.lado, x => ({
                      ...x,
                      capitaoAtletaId: !e.target.checked && x.capitaoAtletaId === a.atletaId ? null : x.capitaoAtletaId,
                      atletas: x.atletas.map(y => (y.atletaId === a.atletaId ? { ...y, selecionado: e.target.checked, titular: e.target.checked ? y.titular : false } : y)),
                    }))} />
                  <span className={styles.nomeAtleta}>{a.nome}</span>
                  {podeAcrescentar && !a.selecionado ? (
                    <>
                      <input type="text" inputMode="numeric" maxLength={2} placeholder="Camisa" aria-label={`Camisa de ${a.nome}`}
                        className={styles.camisa} disabled={salvando || cheio} value={camisaNova[a.atletaId] ?? ''}
                        onChange={e => setCamisaNova(p => ({ ...p, [a.atletaId]: e.target.value.replace(/\D/g, '') }))} />
                      <button className={`${base.btnPrimary} ${base.btnSm}`} style={{ gridColumn: 'span 2' }}
                        disabled={salvando || cheio || !CAMISA_VALIDA.test(camisaNova[a.atletaId] ?? '')}
                        title={cheio ? `Máximo de ${regras.maximo} relacionados` : 'Acrescenta o atleta à súmula (entra no banco)'}
                        onClick={() => acao(async () => {
                          await acrescentarAtleta(dados.sumula!.id, { lado: f.lado, atletaId: a.atletaId, numero: camisaNova[a.atletaId] });
                          setCamisaNova(p => ({ ...p, [a.atletaId]: '' }));
                        }, `${a.nome} acrescentado à súmula.`)}>
                        + Acrescentar
                      </button>
                    </>
                  ) : (<>
                  <input type="text" inputMode="numeric" maxLength={2} placeholder="—" aria-label={`Camisa de ${a.nome}`}
                    className={`${styles.camisa} ${erros[i] ? styles.camisaErro : ''}`} disabled={desabilitado || !a.selecionado} value={a.numero}
                    onChange={e => atualiza(f.lado, x => ({ ...x, atletas: x.atletas.map(y => (y.atletaId === a.atletaId ? { ...y, numero: e.target.value.replace(/\D/g, '') } : y)) }))} />
                  <input type="checkbox" checked={a.selecionado && a.titular} aria-label={`${a.nome} titular`}
                    disabled={desabilitado || !a.selecionado || (!a.titular && titulares >= regras.titulares)}
                    onChange={e => atualiza(f.lado, x => ({ ...x, atletas: x.atletas.map(y => (y.atletaId === a.atletaId ? { ...y, titular: e.target.checked } : y)) }))} />
                  <input type="radio" name={`cap-${idx}`} checked={a.selecionado && f.capitaoAtletaId === a.atletaId} aria-label={`${a.nome} capitão`}
                    disabled={desabilitado || !a.selecionado}
                    onChange={() => atualiza(f.lado, x => ({ ...x, capitaoAtletaId: a.atletaId }))} />
                  </>)}
                  {a.chegouNoPeriodo != null && <span className={styles.tagChegada} title="Acrescentado depois do início do jogo">chegou no {a.chegouNoPeriodo}º período</span>}
                  {erros[i] && <div className={styles.erroLinha}>{erros[i]}</div>}
                </div>
              ))}
            </div>
          );
        })}
      </div>

      {aviso && <div className={`${base.toast} ${aviso.erro ? base.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

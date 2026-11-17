import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type AtletaInscrito,
  type DetalheEquipeInscrita,
  type EquipeInscrita,
  type EquipesInscritasDaTemporada,
  type SituacaoEquipeInscrita,
  aprovarEquipeInscrita,
  listarEquipesInscritas,
  obterEquipeInscrita,
  recusarEquipeInscrita,
  registrarTaxasDaEquipe,
} from '../../api/equipesInscritas';
import DataInput, { dataValida, ddmmParaIso, hojeDdmm } from '../../components/DataInput/DataInput';
import { formatarData, formatarMoeda } from '../../utils/formatacao';
import Modal from './Modal';
import styles from './Inscricoes.module.css';

const MINIMO = 5;
const SITUACOES: SituacaoEquipeInscrita[] = ['AguardandoConfirmacoes', 'EmAnalise', 'Aprovada', 'Recusada'];
const ROTULO: Record<SituacaoEquipeInscrita, string> = {
  AguardandoConfirmacoes: 'Aguardando confirmações', EmAnalise: 'Em análise', Aprovada: 'Aprovada', Recusada: 'Recusada',
};
const ESTILO: Record<SituacaoEquipeInscrita, string> = {
  AguardandoConfirmacoes: styles.stPendente, EmAnalise: styles.stAguardandoPagamento, Aprovada: styles.stEfetivada, Recusada: styles.stRecusada,
};

type ModalAberto =
  | { tipo: 'atletas'; equipe: EquipeInscrita }
  | { tipo: 'aprovar'; equipe: EquipeInscrita }
  | { tipo: 'recusar'; equipe: EquipeInscrita }
  | { tipo: 'taxa'; equipe: EquipeInscrita; atletas: AtletaInscrito[]; taxa: number | null };

/** Equipes inscritas em um torneio aberto: acompanhar as confirmações, aprovar ou recusar e registrar a taxa de cada atleta (que o coloca no elenco). */
export default function EquipesInscritas() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [dados, setDados] = useState<EquipesInscritasDaTemporada | null>(null);
  const [erro, setErro] = useState('');
  const [filtro, setFiltro] = useState<SituacaoEquipeInscrita | 'Todas'>('Todas');
  const [modal, setModal] = useState<ModalAberto | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      setDados(await listarEquipesInscritas(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar as equipes inscritas.');
    }
  }, [id]);

  useEffect(() => { void carregar(); }, [carregar]);

  const equipes = dados?.equipes ?? [];
  const contagem = useMemo(() => {
    const c: Record<string, number> = {};
    for (const e of equipes) c[e.situacao] = (c[e.situacao] ?? 0) + 1;
    return c;
  }, [equipes]);
  const lista = filtro === 'Todas' ? equipes : equipes.filter(e => e.situacao === filtro);

  function avisar(texto: string, comErro = false) {
    setAviso({ texto, erro: comErro });
    window.setTimeout(() => setAviso(null), 6000);
  }

  if (erro) return <div className={styles.page}><p className={styles.erroGlobal}>{erro}</p></div>;
  if (!dados) return <div className={styles.page}><p>Carregando…</p></div>;
  const t = dados.temporada;

  async function abrirTaxa(equipe: EquipeInscrita, atletas: AtletaInscrito[], taxa: number | null) {
    setModal({ tipo: 'taxa', equipe, atletas, taxa });
  }

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/temporadas/${t.id}`)}>‹ Voltar</button>
      </div>
      <h1 className={styles.titulo}>Equipes inscritas — {t.campeonato} {t.ano}</h1>
      <p className={styles.sub}>
        Torneio <b>{t.aberta ? 'aberto' : 'de associados'}</b>: participar {t.aberta ? 'não cria vínculo de associado' : 'cria o vínculo de associado na temporada'}.
        {t.taxaInscricao != null ? <> Taxa de <b>{formatarMoeda(t.taxaInscricao)}</b> por atleta.</> : ' A taxa de inscrição ainda não foi definida.'} Elenco de 5 a 15 atletas.
      </p>
      <div className={styles.banner + ' ' + styles.bannerOn}>
        Fluxo: o responsável inscreve a equipe e convida os atletas → cada atleta confirma → <b>você aprova a equipe</b> → você registra a <b>taxa de cada atleta</b>:
        ao registrar, o atleta fica <b>efetivado e entra no elenco da equipe</b> sozinho. A equipe não espera todos pagarem.
      </div>

      {aviso && <div className={aviso.erro ? styles.toastErro : styles.toast} role={aviso.erro ? 'alert' : 'status'}>{aviso.texto}</div>}

      <div className={styles.tiles}>
        {SITUACOES.map(s => (
          <button key={s} className={`${styles.tile} ${filtro === s ? styles.tileOn : ''}`} onClick={() => setFiltro(filtro === s ? 'Todas' : s)}>
            <span className={styles.tileN}>{contagem[s] ?? 0}</span><span className={styles.tileL}>{ROTULO[s]}</span>
          </button>
        ))}
      </div>
      <div className={styles.tabs}>
        <button className={`${styles.tab} ${filtro === 'Todas' ? styles.tabAtiva : ''}`} onClick={() => setFiltro('Todas')}>Todas<small>{equipes.length}</small></button>
        {SITUACOES.map(s => (
          <button key={s} className={`${styles.tab} ${filtro === s ? styles.tabAtiva : ''}`} onClick={() => setFiltro(s)}>{ROTULO[s]}<small>{contagem[s] ?? 0}</small></button>
        ))}
      </div>

      {equipes.length === 0 ? (
        <div className={styles.vazio}>Nenhuma equipe inscrita ainda. As equipes aparecem aqui quando um responsável as inscreve pelo site.</div>
      ) : (
        <div className={styles.tabelaWrap}>
          <table className={styles.tabela}>
            <thead><tr><th>Equipe</th><th>Categoria</th><th>Responsável</th><th>Elenco</th><th>Situação</th><th /></tr></thead>
            <tbody>
              {lista.map(e => {
                const pendente = e.situacao === 'AguardandoConfirmacoes' || e.situacao === 'EmAnalise';
                const aprovavel = pendente && e.confirmados >= MINIMO;
                return (
                  <tr key={e.id} data-equipe={e.nome}>
                    <td><b>{e.nome}</b><div className={styles.mutado}>#{e.protocolo} · enviada em {formatarData(e.enviadaEm)}</div></td>
                    <td>{e.categoria}</td>
                    <td>{e.responsavel}<div className={styles.mutado}>{e.responsavelEmail}{e.responsavelTelefone ? ` · ${e.responsavelTelefone}` : ''}</div></td>
                    <td>{e.confirmados} confirmados{e.emAberto > 0 ? `, ${e.emAberto} sem resposta` : ''}</td>
                    <td><span className={`${styles.selo} ${ESTILO[e.situacao]}`}>{ROTULO[e.situacao]}</span></td>
                    <td className={styles.direita}>
                      <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'atletas', equipe: e })}>Ver atletas e taxas</button>{' '}
                      {pendente && (
                        <>
                          <button className={`${styles.btnPrimary} ${styles.btnSm}`} disabled={!aprovavel} title={aprovavel ? '' : `Precisa de ${MINIMO} atletas confirmados`}
                            onClick={() => setModal({ tipo: 'aprovar', equipe: e })}>Aprovar</button>{' '}
                          <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} onClick={() => setModal({ tipo: 'recusar', equipe: e })}>Recusar</button>
                        </>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <p className={styles.dica}>Inscrições individuais (atletas sem equipe) continuam na tela de Inscrições: ao serem efetivadas, você as coloca em uma equipe pela tela de Equipes.</p>

      {modal?.tipo === 'atletas' && (
        <ModalAtletas equipe={modal.equipe} onFechar={() => setModal(null)} onRegistrarTaxa={abrirTaxa} />
      )}
      {modal?.tipo === 'aprovar' && (
        <ModalAprovarEquipe equipe={modal.equipe} onFechar={() => setModal(null)}
          onConfirmar={async justificativa => {
            await aprovarEquipeInscrita(modal.equipe.id, justificativa);
            setModal(null); await carregar();
            avisar('Equipe aprovada. Registre a taxa de cada atleta quando receber.');
          }} />
      )}
      {modal?.tipo === 'recusar' && (
        <ModalRecusarEquipe equipe={modal.equipe} onFechar={() => setModal(null)}
          onConfirmar={async motivo => {
            await recusarEquipeInscrita(modal.equipe.id, motivo);
            setModal(null); await carregar();
            avisar('Equipe recusada.');
          }} />
      )}
      {modal?.tipo === 'taxa' && (
        <ModalTaxaEmLote equipe={modal.equipe} atletas={modal.atletas} taxa={modal.taxa} onFechar={() => setModal(null)}
          onConcluir={async (ok, falhas) => {
            setModal(null); await carregar();
            if (falhas.length > 0) avisar(`${ok} taxa(s) registrada(s). Não foi possível registrar: ${falhas.join('; ')}`, true);
            else avisar(`${ok} taxa${ok === 1 ? ' registrada' : 's registradas'}: ${ok === 1 ? 'atleta efetivado e no elenco' : 'atletas efetivados e no elenco'}.`);
          }} />
      )}
    </div>
  );
}

// ---------------------------------------------------------------- atletas e taxas

function ModalAtletas({ equipe, onFechar, onRegistrarTaxa }: {
  equipe: EquipeInscrita; onFechar: () => void; onRegistrarTaxa: (e: EquipeInscrita, atletas: AtletaInscrito[], taxa: number | null) => void;
}) {
  const [d, setD] = useState<DetalheEquipeInscrita | null>(null);
  const [erro, setErro] = useState('');
  const [marcados, setMarcados] = useState<Set<string>>(new Set());

  useEffect(() => { void obterEquipeInscrita(equipe.id).then(setD).catch(e => setErro(e instanceof Error ? e.message : 'Erro ao carregar os atletas.')); }, [equipe.id]);

  const aprovada = d?.situacao === 'Aprovada';
  const podePagar = (a: AtletaInscrito) => aprovada && a.status === 'Confirmado' && a.fichaId !== null && !a.taxaPaga && a.pedido === 'AguardandoPagamento';
  const pagaveis = d?.atletas.filter(podePagar) ?? [];
  const selecionados = pagaveis.filter(a => marcados.has(a.id));

  function alternar(a: AtletaInscrito, on: boolean) {
    setMarcados(prev => { const n = new Set(prev); if (on) n.add(a.id); else n.delete(a.id); return n; });
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={`${styles.modal} ${styles.modalLargo}`} style={{ maxWidth: 760 }}>
        <h2 className={styles.modalTitulo}>{equipe.nome}</h2>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        {!d && !erro && <p>Carregando…</p>}
        {d && (
          <>
            <p className={styles.modalTexto}>{d.categoria} · responsável {d.responsavel.nome} · {d.atletas.length} atletas (máximo {d.maximo})</p>
            {!aprovada && <div className={styles.aviso}>A taxa só pode ser registrada depois que a equipe for <b>aprovada</b>{d.situacao === 'Recusada' ? ' (esta foi recusada).' : '.'}</div>}
            {d.situacao === 'Recusada' && d.motivoRecusa && <p className={styles.modalTexto}>Motivo da recusa: {d.motivoRecusa}</p>}
            <div className={styles.tabelaWrap}>
              <table className={styles.tabela}>
                <thead>
                  <tr>
                    <th style={{ width: 28 }}>
                      <input type="checkbox" aria-label="Selecionar todos os que podem pagar" disabled={pagaveis.length === 0}
                        checked={pagaveis.length > 0 && selecionados.length === pagaveis.length}
                        onChange={e => setMarcados(e.target.checked ? new Set(pagaveis.map(a => a.id)) : new Set())} />
                    </th>
                    <th>Atleta</th><th>Convite</th><th>Taxa</th><th>Elenco</th>
                  </tr>
                </thead>
                <tbody>
                  {d.atletas.map(a => (
                    <tr key={a.id} data-atleta={a.nome}>
                      <td><input type="checkbox" aria-label={`Selecionar ${a.nome}`} disabled={!podePagar(a)} checked={marcados.has(a.id)} onChange={e => alternar(a, e.target.checked)} /></td>
                      <td><b>{a.nome}</b>{a.ehResponsavel ? ' (responsável)' : ''}<div className={styles.mutado}>{a.email}{a.cpf ? ` · ${a.cpf}` : ''}</div></td>
                      <td><span className={`${styles.selo} ${a.status === 'Confirmado' ? styles.stEfetivada : a.status === 'Enviado' ? styles.stPendente : styles.stRecusada}`}>{a.status === 'Enviado' ? 'Sem resposta' : a.status === 'Confirmado' ? 'Confirmou' : a.status === 'Recusou' ? 'Recusou' : 'Vencido'}</span></td>
                      <td>{a.taxaPaga ? <span className={`${styles.selo} ${styles.stEfetivada}`}>Paga {d.taxa != null ? formatarMoeda(d.taxa) : ''}</span> : a.pedido === 'AguardandoPagamento' ? <span className={`${styles.selo} ${styles.stAguardandoPagamento}`}>A pagar</span> : <span className={styles.mutado}>—</span>}</td>
                      <td>{a.naEquipe ? <span className={`${styles.selo} ${styles.stEfetivada}`}>no elenco</span> : <span className={styles.mutado}>—</span>}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className={styles.modalFooter} style={{ justifyContent: 'space-between', alignItems: 'center' }}>
              <span className={styles.modalTexto} style={{ margin: 0 }}>
                {selecionados.length > 0
                  ? <><b>{selecionados.length} selecionado{selecionados.length > 1 ? 's' : ''}</b>{d.taxa != null ? <> · {selecionados.length} × {formatarMoeda(d.taxa)} = <b>{formatarMoeda(selecionados.length * d.taxa)}</b></> : null}</>
                  : aprovada ? 'Marque os atletas cuja taxa foi recebida.' : ''}
              </span>
              <span style={{ display: 'flex', gap: 8 }}>
                <button className={styles.btnSecondary} onClick={onFechar}>Fechar</button>
                <button className={styles.btnPrimary} id="registrar-taxas" disabled={selecionados.length === 0} onClick={() => onRegistrarTaxa(equipe, selecionados, d.taxa)}>Registrar taxa dos selecionados</button>
              </span>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function ModalTaxaEmLote({ equipe, atletas, taxa, onFechar, onConcluir }: {
  equipe: EquipeInscrita; atletas: AtletaInscrito[]; taxa: number | null; onFechar: () => void; onConcluir: (ok: number, falhas: string[]) => Promise<void>;
}) {
  const [data, setData] = useState(hojeDdmm());
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function confirmar() {
    if (!dataValida(data)) { setErro('Informe uma data válida.'); return; }
    setSalvando(true); setErro('');
    try {
      const r = await registrarTaxasDaEquipe(equipe.id, ddmmParaIso(data), atletas.map(a => a.fichaId!));
      const falhas = r.resultados.filter(x => !x.ok).map(x => `${atletas.find(a => a.fichaId === x.inscricaoId)?.nome ?? 'atleta'}: ${x.erro}`);
      await onConcluir(r.resultados.length - falhas.length, falhas);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao registrar as taxas.');
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Registrar taxa de inscrição" erro={erro} salvando={salvando} rotuloConfirmar="Registrar" onConfirmar={() => void confirmar()} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        Registrar a taxa de <b>{atletas.length} atleta{atletas.length > 1 ? 's' : ''}</b> da equipe <b>{equipe.nome}</b>
        {taxa != null ? <>: total de <b>{formatarMoeda(atletas.length * taxa)}</b></> : null}.
      </p>
      <div className={styles.field}>
        <label htmlFor="taxa-data">Data do pagamento</label>
        <DataInput id="taxa-data" value={data} onChange={setData} />
      </div>
      <ul className={styles.mutado}>{atletas.map(a => <li key={a.id}>{a.nome}{taxa != null ? ` — ${formatarMoeda(taxa)}` : ''}</li>)}</ul>
      <div className={`${styles.banner} ${styles.bannerOn}`}>Cada atleta fica <b>efetivado</b> e entra no elenco da equipe. Um e-mail avisa quem se inscreveu pelo site.</div>
    </Modal>
  );
}

// ---------------------------------------------------------------- aprovar e recusar

function ModalAprovarEquipe({ equipe, onFechar, onConfirmar }: { equipe: EquipeInscrita; onFechar: () => void; onConfirmar: (justificativa?: string) => Promise<void> }) {
  const [justificativa, setJustificativa] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  // O servidor avisa quais atletas estão fora do esperado; só então a justificativa passa a ser exigida.
  const [pedeJustificativa, setPedeJustificativa] = useState(false);

  async function confirmar() {
    setSalvando(true); setErro('');
    try {
      await onConfirmar(justificativa.trim() || undefined);
    } catch (e) {
      const m = e instanceof Error ? e.message : 'Erro ao aprovar a equipe.';
      if (m.includes('justificativa')) setPedeJustificativa(true);
      setErro(m);
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Aprovar equipe" erro={erro} salvando={salvando} rotuloConfirmar="Aprovar equipe" onConfirmar={() => void confirmar()} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        A equipe <b>{equipe.nome}</b> tem <b>{equipe.confirmados} atletas confirmados</b> (mínimo {MINIMO}).
        {equipe.emAberto > 0 ? ` Os convites sem resposta (${equipe.emAberto}) seguem abertos até o prazo.` : ''}
      </p>
      <div className={`${styles.banner} ${styles.bannerOn}`}>Aprovar não cobra nada. Os atletas confirmados passam a <b>aguardar a taxa</b>; cada atleta entra no elenco quando a sua taxa é registrada.</div>
      {pedeJustificativa && (
        <div className={styles.field}>
          <label htmlFor="eq-just">Justificativa da diretoria (atleta fora do esperado para a categoria)</label>
          <textarea id="eq-just" className={styles.textarea} rows={3} maxLength={500} value={justificativa} onChange={e => { setJustificativa(e.target.value); setErro(''); }} />
        </div>
      )}
    </Modal>
  );
}

function ModalRecusarEquipe({ equipe, onFechar, onConfirmar }: { equipe: EquipeInscrita; onFechar: () => void; onConfirmar: (motivo: string) => Promise<void> }) {
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function confirmar() {
    if (!motivo.trim()) { setErro('Informe o motivo da recusa.'); return; }
    setSalvando(true); setErro('');
    try {
      await onConfirmar(motivo.trim());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao recusar a equipe.');
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Recusar equipe" erro={erro} salvando={salvando} rotuloConfirmar="Recusar equipe" onConfirmar={() => void confirmar()} onFechar={onFechar}>
      <p className={styles.modalTexto}>Recusar a equipe <b>{equipe.nome}</b>. O responsável é avisado, os pedidos dos atletas que já confirmaram são recusados e nada foi cobrado.</p>
      <div className={styles.field}>
        <label htmlFor="eq-motivo">Motivo (obrigatório; o responsável vê)</label>
        <input id="eq-motivo" className={styles.input} maxLength={500} placeholder="Ex.: categoria completa neste torneio" value={motivo} onChange={e => { setMotivo(e.target.value); setErro(''); }} />
      </div>
    </Modal>
  );
}

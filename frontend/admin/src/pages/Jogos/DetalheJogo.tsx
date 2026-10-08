import { useCallback, useEffect, useState } from 'react';
import EncerrarSumula from '../Sumula/EncerrarSumula';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type DetalheJogo as Detalhe,
  type LadoJogo,
  type StatusJogo,
  type StatusSumula,
  linksDaSumula,
  obterDetalheJogo,
  prepararSumula,
  registrarWO,
} from '../../api/jogos';
import { ModalAgendar } from '../Fases/DetalheFase';
import base from '../Fases/Fases.module.css';
import styles from './Jogos.module.css';

const STATUS_ROTULO: Record<StatusJogo, string> = {
  Agendado: 'Agendado', EmAndamento: 'Em andamento', Encerrado: 'Encerrado', WO: 'W.O.', Dispensado: 'Dispensado',
};
const STATUS_ESTILO: Record<StatusJogo, string> = {
  Agendado: base.stPlanejada, EmAndamento: base.stEmAndamento, Encerrado: base.stEncerrada, WO: base.stEmAndamento, Dispensado: base.stPlanejada,
};
const SUMULA_ROTULO: Record<StatusSumula, string> = { EmPreparacao: 'Em preparação', EmAndamento: 'Em andamento', Intervalo: 'Intervalo', Encerrada: 'Encerrada' };

const DIAS = ['domingo', 'segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado'];

function dataLonga(iso: string | null): string {
  if (!iso) return 'a definir';
  const [y, m, d] = iso.split('-').map(Number);
  return `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}/${y} (${DIAS[new Date(y, m - 1, d).getDay()]})`;
}

function Equipe({ lado }: { lado: LadoJogo }) {
  return <div className={`${styles.eqNome} ${lado.definida ? '' : styles.eqRef}`}>{lado.texto}</div>;
}

/**
 * Detalhe do jogo no campeonato: equipes (ou a origem, se ainda indefinidas), agendamento, status e resultado, W.O. e o
 * resumo da súmula. A relação dos jogadores, o técnico e o capitão ficam na súmula (tela própria).
 */
export default function DetalheJogo() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<Detalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [agendando, setAgendando] = useState(false);
  const [wo, setWo] = useState<{ casaAusente: boolean } | null>(null);
  const [woErro, setWoErro] = useState('');
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      setDados(await obterDetalheJogo(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar o jogo.');
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

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !dados || !id) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Jogo não encontrado.'}</p></div>;

  const placar = dados.placarCasa != null && dados.placarVisitante != null ? `${dados.placarCasa} × ${dados.placarVisitante}` : '×';
  const sumula = dados.sumula;
  const wTime = dados.woCasaAusente == null ? null : (dados.woCasaAusente ? dados.casa.texto : dados.visitante.texto);

  async function preparar() {
    try {
      await prepararSumula(id!);
      navigate(`/jogos/${id}/sumula`);
    } catch (e) {
      setAviso({ texto: e instanceof Error ? e.message : 'Não foi possível preparar a súmula.', erro: true });
    }
  }

  async function confirmarWO() {
    if (!wo) return;
    setWoErro('');
    try {
      await registrarWO(id!, wo.casaAusente);
      setWo(null);
      setAviso({ texto: 'W.O. registrado.', erro: false });
      await carregar();
    } catch (e) {
      setWoErro(e instanceof Error ? e.message : 'Não foi possível registrar o W.O.');
    }
  }

  return (
    <div className={base.page}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(`/temporadas/${dados.temporada.id}/jogos`)}>‹ Voltar aos jogos</button>
      </div>

      <h1 className={base.titulo}>Jogo #{dados.numero} — {dados.categoria.nome} · {dados.etiqueta}</h1>
      <p className={base.sub}>{dados.fase.nome} · Temporada {dados.temporada.ano}</p>

      <div className={styles.cartaoJogo}>
        <div className={styles.placarGrande}>
          <Equipe lado={dados.casa} />
          <div>
            <div className={styles.pts}>{placar}</div>
            {dados.status === 'WO' && <div className={base.dica}>W.O.</div>}
          </div>
          <Equipe lado={dados.visitante} />
        </div>
        <div className={styles.metaJogo}>
          <span className={`${base.status} ${STATUS_ESTILO[dados.status]}`}>{STATUS_ROTULO[dados.status]}</span>
          <span className={styles.tagCat}>{dados.categoria.nome}</span>
          <span className={styles.tag}>{dados.fase.nome} · {dados.etiqueta}</span>
          {dados.opcional && <span className={styles.opcional}>se necessário</span>}
        </div>
      </div>

      {!dados.casa.definida || !dados.visitante.definida ? (
        <div className={`${base.banner} ${base.bannerAviso}`}><span><b>Equipes ainda não definidas.</b> A súmula só pode ser preparada quando as fases de origem terminarem.</span></div>
      ) : dados.status === 'WO' ? (
        <div className={`${base.banner} ${base.bannerTrava}`}><span><b>W.O. registrado.</b> {wTime} ausente · 20 × 0, sem bonificação.</span></div>
      ) : dados.status === 'Encerrado' ? (
        <div className={`${base.banner} ${base.bannerAberto}`}><span><b>Jogo encerrado.</b> Resultado vindo da súmula.</span></div>
      ) : dados.status === 'EmAndamento' ? (
        <div className={`${base.banner} ${base.bannerTrava}`}><span><b>Jogo em andamento.</b> O placar é atualizado pela súmula.</span></div>
      ) : sumula ? (
        <div className={`${base.banner} ${base.bannerAberto}`}><span><b>Súmula em preparação.</b> Confira a relação e inicie quando o jogo começar.</span></div>
      ) : (
        <div className={`${base.banner} ${base.bannerAviso}`}><span><b>Sem súmula.</b> Comece a prepará-la e informe a relação dos jogadores antes do jogo.</span></div>
      )}

      <div className={styles.grade2}>
        <div className={styles.cartaoInfo}>
          <h3>Agendamento</h3>
          <dl className={styles.kv}>
            <dt>Data</dt><dd>{dataLonga(dados.data)}</dd>
            <dt>Horário</dt><dd>{dados.hora ? dados.hora.slice(0, 5) : 'a definir'}</dd>
            <dt>Local</dt><dd>{dados.local ? `${dados.local.nome} — ${dados.local.cidade}${dados.local.estado ? `/${dados.local.estado}` : ''}` : 'a definir'}</dd>
            <dt>Rodada</dt><dd>{dados.rodada}{dados.jogoDaSerie ? ` · jogo ${dados.jogoDaSerie} da série` : ''}</dd>
          </dl>
          {dados.podeAgendar && (
            <div style={{ marginTop: 10 }}>
              <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => setAgendando(true)}>Alterar agendamento</button>
            </div>
          )}
        </div>

        <div className={styles.cartaoInfo}>
          <h3>
            Súmula
            <span className={`${base.status} ${sumula ? (sumula.status === 'Encerrada' ? base.stEncerrada : sumula.status === 'EmPreparacao' ? styles.stPreparada : base.stEmAndamento) : base.stPlanejada}`} style={{ marginLeft: 8 }}>
              {sumula ? SUMULA_ROTULO[sumula.status] : 'Sem súmula'}
            </span>
          </h3>
          {sumula ? (
            <>
              {sumula.times.map(t => (
                <div key={t.lado} className={styles.resumoTime}>
                  <b>{t.nome}</b> — {t.relacionados} relacionados
                  <div className={base.dica}>Técnico: {t.tecnico ?? 'não informado'} · Capitão: {t.capitao ?? 'não definido'}</div>
                </div>
              ))}
              <div className={styles.acoesSumula}>
                <button className={base.btnPrimary} onClick={() => navigate(`/jogos/${id}/sumula`)}>
                  {sumula.status === 'EmPreparacao' ? 'Continuar preparação ›' : 'Abrir súmula ›'}
                </button>
                <button className={base.btnSecondary} onClick={() => navigate(`/jogos/${id}/sumula/importar-fiba`)}>
                  Importar do FIBA LiveStats ›
                </button>
                {/* Aqui a gestão só acompanha o placar; o mesário opera pela área do árbitro. */}
                {sumula.status !== 'EmPreparacao' && (() => {
                  const l = linksDaSumula(sumula.id);
                  return (
                    <>
                      <a className={`${base.btnSecondary} ${styles.link}`} href={l.placar} target="_blank" rel="noreferrer">Placar</a>
                      {sumula.status !== 'Encerrada' && (
                        <EncerrarSumula sumulaId={sumula.id} jogo={`#${dados.numero} — ${dados.casa.texto} × ${dados.visitante.texto}`} onEncerrada={carregar} />
                      )}
                    </>
                  );
                })()}
              </div>
            </>
          ) : dados.status === 'WO' ? (
            <p className={base.dica}>Sem súmula: o jogo foi decidido por W.O.</p>
          ) : (
            <>
              <p className={base.dica}>{dados.motivoSemSumula ?? 'Nenhuma súmula criada para este jogo.'}</p>
              <button className={base.btnPrimary} disabled={!dados.podePrepararSumula} onClick={preparar}>Preparar súmula ›</button>
            </>
          )}
        </div>
      </div>

      <div className={styles.cartaoInfo}>
        <h3>Resultado</h3>
        {dados.status === 'Encerrado' && <p>{dados.casa.texto} <b>{dados.placarCasa}</b> × <b>{dados.placarVisitante}</b> {dados.visitante.texto}</p>}
        {dados.status === 'WO' && <p>W.O. — vitória de {dados.woCasaAusente ? dados.visitante.texto : dados.casa.texto} por 20 × 0 (sem bonificação).</p>}
        {dados.status !== 'Encerrado' && dados.status !== 'WO' && <p className={base.dica}>Sem resultado ainda.</p>}
        {dados.podeRegistrarWO && (
          <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={() => { setWoErro(''); setWo({ casaAusente: true }); }}>Registrar W.O.…</button>
        )}
      </div>

      {agendando && (
        <ModalAgendar
          jogo={dados}
          titulo={`${dados.casa.texto} × ${dados.visitante.texto} · ${dados.categoria.nome}`}
          associacaoId={dados.associacaoId}
          onFechar={() => setAgendando(false)}
          onSalvo={async () => { setAgendando(false); setAviso({ texto: 'Jogo agendado.', erro: false }); await carregar(); }}
        />
      )}

      {wo && (
        <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setWo(null); }}>
          <div className={base.modal} style={{ maxWidth: 460 }}>
            <h2 className={base.modalTitulo}>Registrar W.O.</h2>
            <p className={base.modalTexto}>
              Qual equipe não compareceu (ou não apresentou ao menos 7 atletas uniformizados 15 minutos após o horário)? A outra vence por 20 × 0, sem
              bonificação. Se a súmula do jogo ainda estiver em preparação, ela será descartada.
            </p>
            <label className={styles.radio}><input type="radio" checked={wo.casaAusente} onChange={() => setWo({ casaAusente: true })} /> {dados.casa.texto} (casa)</label>
            <label className={styles.radio}><input type="radio" checked={!wo.casaAusente} onChange={() => setWo({ casaAusente: false })} /> {dados.visitante.texto} (visitante)</label>
            {woErro && <p className={base.fieldErro}>{woErro}</p>}
            <div className={base.modalFooter}>
              <button className={base.btnSecondary} onClick={() => setWo(null)}>Cancelar</button>
              <button className={base.btnPrimary} onClick={confirmarWO}>Registrar W.O.</button>
            </div>
          </div>
        </div>
      )}

      {aviso && <div className={`${base.toast} ${aviso.erro ? base.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

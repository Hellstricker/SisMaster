import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { type DetalheJogo, obterDetalheJogo } from '../../api/jogos';
import { ErroDaApi } from '../../api/http';
import {
  type EventoDeEquipeFiba,
  type PreviaImportacaoFiba,
  type PreviaTimeFiba,
  importarFiba,
  previaImportacaoFiba,
} from '../../api/importacaoFiba';
import base from '../Fases/Fases.module.css';
import styles from './ImportarFiba.module.css';

type Etapa = 'codigo' | 'previa' | 'importada';

/** Só o código do jogo no LiveStats (o número do endereço …/u/CBBC/2888968/bs.html). */
const CODIGO_VALIDO = /^\d{1,12}$/;

const ROTULO_LADO = { Casa: 'Casa', Visitante: 'Visitante' } as const;

const PERIODO_ROTULO: Record<string, string> = { Primeiro: '1º', Segundo: '2º', Terceiro: '3º', Quarto: '4º', Prorrogacao: 'Prorrogação' };
const TIPO_EQUIPE_ROTULO: Record<string, string> = {
  ReboteOfensivo: 'Rebote ofensivo', ReboteDefensivo: 'Rebote defensivo', Turnover: 'Turnover',
  FaltaTecnica: 'Falta técnica', FaltaPessoal: 'Falta pessoal', FaltaAntiDesportiva: 'Falta antidesportiva',
};

const mmss = (segundos: number) => `${String(Math.floor(segundos / 60)).padStart(2, '0')}:${String(segundos % 60).padStart(2, '0')}`;

/** "5 rebotes defensivos, 1 rebote ofensivo, 2 turnovers" — o resumo dos lances de equipe de um time. */
function resumoDeEquipe(lances: EventoDeEquipeFiba[]): string {
  const por = (tipo: string) => lances.filter(l => l.tipo === tipo).length;
  const plural = (n: number, um: string, varios: string) => `${n} ${n === 1 ? um : varios}`;
  const partes = [
    por('ReboteDefensivo') && plural(por('ReboteDefensivo'), 'rebote defensivo', 'rebotes defensivos'),
    por('ReboteOfensivo') && plural(por('ReboteOfensivo'), 'rebote ofensivo', 'rebotes ofensivos'),
    por('Turnover') && plural(por('Turnover'), 'turnover', 'turnovers'),
  ].filter(Boolean);
  const outros = lances.filter(l => !TIPO_EQUIPE_ROTULO[l.tipo] || !['ReboteDefensivo', 'ReboteOfensivo', 'Turnover'].includes(l.tipo)).length;
  if (outros) partes.push(plural(outros, 'outro lance', 'outros lances'));
  return partes.join(', ') || 'nenhum';
}

function situacao(j: PreviaTimeFiba['jogadores'][number]): { nivel: 'ok' | 'bad' | 'warn'; texto: string } {
  if (j.nomeSumula === null) {
    return j.jogou
      ? { nivel: 'bad', texto: 'Jogou e não está relacionada' }
      : { nivel: 'warn', texto: 'Não jogou e não está relacionada' };
  }
  if (j.titularSumula !== null && j.titularSumula !== j.titularFeed) {
    return { nivel: 'warn', texto: j.titularFeed ? 'Titular no feed, reserva na súmula — será ajustado ao feed' : 'Reserva no feed, titular na súmula — será ajustado ao feed' };
  }
  if (j.divergencias.length) return { nivel: 'bad', texto: j.divergencias.join('; ') };
  return { nivel: 'ok', texto: 'Confere' };
}

/**
 * Tela 11C — importa, de um jogo já realizado no FIBA LiveStats, os lances e as trocas da súmula. Primeiro confere (nada é
 * gravado), depois, com confirmação, substitui a súmula e encerra o jogo. Só a gestão (diretoria/administrador) tem esta tela.
 */
export default function ImportarFiba() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [jogo, setJogo] = useState<DetalheJogo | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');

  const [codigo, setCodigo] = useState('');
  const [inverter, setInverter] = useState(false);
  const [etapa, setEtapa] = useState<Etapa>('codigo');
  const [previa, setPrevia] = useState<PreviaImportacaoFiba | null>(null);
  /** Código e lados com os quais a prévia foi feita: é o que será importado, mesmo que o formulário mude depois. */
  const [conferido, setConferido] = useState<{ codigo: string; inverter: boolean } | null>(null);
  const [erroCodigo, setErroCodigo] = useState('');
  const [conferindo, setConferindo] = useState(false);
  const [confirmando, setConfirmando] = useState(false);
  const [importando, setImportando] = useState(false);
  const [errosImportacao, setErrosImportacao] = useState<string[]>([]);

  useEffect(() => {
    if (!id) return;
    obterDetalheJogo(id)
      .then(setJogo)
      .catch(e => setErro(e instanceof Error ? e.message : 'Não foi possível carregar o jogo.'))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !jogo || !id) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Jogo não encontrado.'}</p></div>;

  const sumula = jogo.sumula;
  const titulo = `Jogo #${jogo.numero} — ${jogo.casa.texto} × ${jogo.visitante.texto}`;
  const semSumula = !sumula || jogo.status === 'WO' || jogo.status === 'Dispensado';

  async function conferir() {
    const c = codigo.trim();
    if (!CODIGO_VALIDO.test(c)) {
      setErroCodigo('Informe o código do jogo no LiveStats, só números (ex.: 2888968).');
      return;
    }
    setErroCodigo('');
    setConferindo(true);
    try {
      setPrevia(await previaImportacaoFiba(sumula!.id, c, inverter));
      setConferido({ codigo: c, inverter });
      setEtapa('previa');
    } catch (e) {
      setErroCodigo(e instanceof Error ? e.message : 'Não foi possível conferir o jogo.');
    } finally {
      setConferindo(false);
    }
  }

  async function importar() {
    if (!conferido) return;
    setImportando(true);
    setErrosImportacao([]);
    try {
      await importarFiba(sumula!.id, conferido.codigo, conferido.inverter);
      setConfirmando(false);
      setEtapa('importada');
    } catch (e) {
      setErrosImportacao(e instanceof ErroDaApi ? e.mensagens : [e instanceof Error ? e.message : 'Não foi possível importar o jogo.']);
    } finally {
      setImportando(false);
    }
  }

  const [casa, visitante] = previa?.times ?? [];
  const jaTinhaDados = jogo.status !== 'Agendado' || (sumula != null && sumula.status !== 'EmPreparacao');

  return (
    <div className={base.page}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(`/jogos/${id}`)}>‹ Voltar ao jogo</button>
      </div>

      <h1 className={base.titulo}>Importar jogo do FIBA LiveStats</h1>
      <p className={base.sub}>
        {titulo}. Substitui os dados da súmula pelos de um jogo já realizado, tirados do sistema oficial de estatísticas da FIBA.
      </p>

      <div className={styles.passos}>
        {(['codigo', 'previa', 'importada'] as Etapa[]).map((e, i) => {
          const ordem = ['codigo', 'previa', 'importada'].indexOf(etapa);
          const classe = etapa === 'importada' || i < ordem ? styles.passoFeito : i === ordem ? styles.passoAtual : '';
          return <span key={e} className={`${styles.passo} ${classe}`}>{['1. Escolher o jogo', '2. Conferir', '3. Confirmar'][i]}</span>;
        })}
      </div>

      {semSumula ? (
        <div className={`${base.banner} ${base.bannerAviso}`}>
          <span>
            {jogo.status === 'WO' || jogo.status === 'Dispensado'
              ? <><b>Este jogo não tem súmula.</b> Jogo de W.O. ou dispensado não recebe importação.</>
              : <><b>Este jogo ainda não tem súmula.</b> Prepare-a e informe a relação dos jogadores antes de importar.</>}
          </span>
        </div>
      ) : etapa === 'importada' ? (
        <>
          <div className={`${base.banner} ${base.bannerAberto}`}>
            <span>
              <b>Súmula importada e jogo encerrado.</b> {casa?.nomeSumula} {casa?.placarFeed} × {visitante?.placarFeed} {visitante?.nomeSumula} —
              {' '}{previa?.eventos} lances e {previa?.trocas} trocas gravados, classificação recalculada. O JSON original da FIBA ficou guardado.
            </span>
          </div>
          <div className={styles.acoes}>
            <button className={base.btnPrimary} onClick={() => navigate(`/jogos/${id}/sumula`)}>Abrir súmula ›</button>
            <button className={base.btnSecondary} onClick={() => navigate(`/jogos/${id}`)}>Voltar ao jogo</button>
          </div>
        </>
      ) : (
        <>
          <div className={styles.cartao}>
            <h3>1. Código do jogo</h3>
            <div className={styles.linhaCodigo}>
              <input
                className={base.input}
                value={codigo}
                inputMode="numeric"
                maxLength={12}
                placeholder="Ex.: 2888968"
                aria-label="Código do jogo no LiveStats"
                onChange={e => { setCodigo(e.target.value); setErroCodigo(''); }}
                onKeyDown={e => { if (e.key === 'Enter' && !conferindo) conferir(); }}
              />
              <button className={base.btnPrimary} disabled={conferindo} onClick={conferir}>{conferindo ? 'Conferindo...' : 'Conferir jogo'}</button>
            </div>
            <p className={base.dica}>Só o código do jogo no LiveStats — o número que aparece no endereço da página (…/u/CBBC/<b>2888968</b>/bs.html).</p>
            <label className={styles.check}>
              <input type="checkbox" checked={inverter} onChange={e => setInverter(e.target.checked)} />
              Inverter casa e visitante
              <span className={base.dica}> — o feed da FIBA costuma trazer a casa primeiro; marque se esta súmula estiver ao contrário.</span>
            </label>
            {erroCodigo && <p className={base.erroGlobal} style={{ marginTop: 10 }}>{erroCodigo}</p>}
          </div>

          {etapa === 'previa' && previa && casa && visitante && conferido && (
            <>
              {conferido.codigo !== codigo.trim() || conferido.inverter !== inverter ? (
                <div className={`${base.banner} ${base.bannerAviso}`}><span>O código ou os lados foram alterados depois da conferência. Confira de novo para importar.</span></div>
              ) : null}

              {previa.podeAplicar ? (
                <div className={`${base.banner} ${base.bannerAberto}`}><span><b>Tudo confere.</b> O jogo reconstruído bate com os números oficiais da FIBA.</span></div>
              ) : (
                <div className={`${base.banner} ${styles.bannerErro}`}>
                  <div>
                    <b>Não dá para importar ainda — {previa.problemas.length} problema(s):</b>
                    <ul className={styles.lista}>{previa.problemas.map(p => <li key={p}>{p}</li>)}</ul>
                    <div className={styles.dicaBanner}>Corrija a relação na Tela da súmula (ou use “Inverter casa e visitante”) e confira de novo.</div>
                  </div>
                </div>
              )}

              {previa.avisos.length > 0 && (
                <div className={`${base.banner} ${base.bannerAviso}`}>
                  <div>
                    <b>Avisos (não impedem):</b>
                    <ul className={styles.lista}>{previa.avisos.map(p => <li key={p}>{p}</li>)}</ul>
                  </div>
                </div>
              )}

              {jaTinhaDados && (
                <div className={`${base.banner} ${base.bannerTrava}`}>
                  <span><b>Esta súmula já tem dados.</b> Os lances, as trocas e o placar lançados antes serão apagados e substituídos.</span>
                </div>
              )}

              <div className={styles.cartao}>
                <div className={styles.placar}>
                  <div className={styles.eq}>{casa.nomeSumula}<small>feed: {casa.nomeFeed}</small></div>
                  <div>
                    <div className={styles.pts}>{casa.placarReconstruido} × {visitante.placarReconstruido}</div>
                    {(() => {
                      const igual = casa.placarReconstruido === casa.placarFeed && visitante.placarReconstruido === visitante.placarFeed;
                      return <div className={igual ? styles.confOk : styles.confRuim}>{igual ? `✓ igual ao oficial (${casa.placarFeed} × ${visitante.placarFeed})` : `✗ oficial: ${casa.placarFeed} × ${visitante.placarFeed}`}</div>;
                    })()}
                  </div>
                  <div className={styles.eq}>{visitante.nomeSumula}<small>feed: {visitante.nomeFeed}</small></div>
                </div>
                <table className={styles.tabela} style={{ marginTop: 12 }}>
                  <thead>
                    <tr><th>Período</th><th className={styles.n}>{casa.nomeSumula} (oficial / reconstruído)</th><th className={styles.n}>{visitante.nomeSumula} (oficial / reconstruído)</th></tr>
                  </thead>
                  <tbody>
                    {casa.periodos.map((p, i) => {
                      const v = visitante.periodos[i];
                      const okC = p.placarFeed == null || p.placarFeed === p.placarReconstruido;
                      const okV = v.placarFeed == null || v.placarFeed === v.placarReconstruido;
                      return (
                        <tr key={p.periodo} className={okC && okV ? '' : styles.linhaErro}>
                          <td>{p.periodo}º período</td>
                          <td className={styles.n}>{p.placarFeed ?? '—'} / {p.placarReconstruido} {okC ? '✓' : '✗'}</td>
                          <td className={styles.n}>{v.placarFeed ?? '—'} / {v.placarReconstruido} {okV ? '✓' : '✗'}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              <div className={styles.colunas}>
                {previa.times.map(t => (
                  <div key={t.lado} className={styles.cartao}>
                    <h3>{t.nomeSumula} <span className={styles.tag}>{ROTULO_LADO[t.lado]}</span></h3>
                    <table className={styles.tabela}>
                      <thead>
                        <tr><th className={styles.n}>Nº</th><th>Atleta (súmula ↔ feed)</th><th className={styles.n}>Tit.</th><th>Situação</th></tr>
                      </thead>
                      <tbody>
                        {t.jogadores.map(j => {
                          const s = situacao(j);
                          return (
                            <tr key={j.camisa} className={s.nivel === 'bad' ? styles.linhaErro : s.nivel === 'warn' ? styles.linhaAviso : ''}>
                              <td className={styles.n}>{j.camisa}</td>
                              <td>
                                {j.nomeSumula ? <b>{j.nomeSumula}</b> : <i className={base.dica}>fora da relação</i>}
                                <div className={base.dica}>feed: {j.nomeFeed}</div>
                              </td>
                              <td className={styles.n}>
                                {j.titularFeed ? '●' : ''}
                                {j.titularSumula !== null && j.titularSumula !== j.titularFeed ? <span className={styles.st_bad}> ({j.titularSumula ? '●' : '○'})</span> : null}
                              </td>
                              <td><span className={styles[`st_${s.nivel}`]}>{s.nivel === 'ok' ? '✓ ' : s.nivel === 'bad' ? '✗ ' : '! '}{s.texto}</span></td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                ))}
              </div>

              {previa.eventosDeEquipe.length > 0 && (
                <div className={styles.cartao}>
                  <h3>Lances de equipe — não entram na súmula <span className={styles.tag}>{previa.eventosDeEquipe.length}</span></h3>
                  <p className={base.dica}>
                    A FIBA registra estes lances para a equipe, não para um jogador (bola que sai da quadra, violação dos 24 segundos).
                    A súmula guarda lances de jogadores, então eles ficam de fora — não mudam o placar nem os totais de nenhum atleta.
                  </p>
                  {previa.times.map(t => (
                    <p key={t.lado} style={{ margin: '4px 0', fontSize: 13 }}>
                      <b>{t.nomeSumula}:</b> {resumoDeEquipe(previa.eventosDeEquipe.filter(l => l.lado === t.lado))}
                    </p>
                  ))}
                  <details className={styles.detalhes}>
                    <summary>Ver os {previa.eventosDeEquipe.length} lances</summary>
                    <table className={styles.tabela}>
                      <thead><tr><th>Período</th><th className={styles.n}>Faltava</th><th>Equipe</th><th>Lance</th></tr></thead>
                      <tbody>
                        {previa.eventosDeEquipe.map((l, i) => (
                          <tr key={i}>
                            <td>{PERIODO_ROTULO[String(l.periodo)] ?? l.periodo}</td>
                            <td className={styles.n}>{mmss(l.tempoRestanteSegundos)}</td>
                            <td>{previa.times.find(t => t.lado === l.lado)?.nomeSumula}</td>
                            <td>{TIPO_EQUIPE_ROTULO[l.tipo] ?? l.tipo}{l.tipo === 'Turnover' ? ' (24 segundos)' : ''}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </details>
                </div>
              )}

              <div className={styles.cartao}>
                <h3>O que será gravado</h3>
                <div className={styles.resumo}>
                  <div><b>{previa.eventos}</b>lances de jogadores</div>
                  <div><b>{previa.trocas}</b>substituições</div>
                  <div><b>{previa.eventosDeEquipe.length}</b>lances de equipe ignorados</div>
                  <div><b>{casa.placarReconstruido} × {visitante.placarReconstruido}</b>placar final</div>
                </div>
              </div>

              <div className={styles.acoes}>
                <button className={base.btnSecondary} onClick={() => { setEtapa('codigo'); setPrevia(null); setConferido(null); }}>Trocar de jogo</button>
                <button
                  className={base.btnPrimary}
                  disabled={!previa.podeAplicar || conferido.codigo !== codigo.trim() || conferido.inverter !== inverter}
                  title={previa.podeAplicar ? undefined : 'Resolva os problemas acima'}
                  onClick={() => { setErrosImportacao([]); setConfirmando(true); }}
                >
                  Importar ›
                </button>
              </div>
            </>
          )}
        </>
      )}

      {confirmando && previa && casa && visitante && conferido && (
        <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget && !importando) setConfirmando(false); }}>
          <div className={base.modal} style={{ maxWidth: 500 }}>
            <h2 className={base.modalTitulo}>Substituir a súmula pelo jogo da FIBA?</h2>
            <p className={base.modalTexto}>Esta ação grava os dados do jogo <b>{conferido.codigo}</b> na súmula do <b>Jogo #{jogo.numero}</b>:</p>
            <ul className={styles.lista}>
              {jaTinhaDados
                ? <li>Os lances, as trocas e o placar já lançados <b>serão apagados</b>.</li>
                : <li>A súmula ainda não tem lances; será iniciada e preenchida.</li>}
              <li>A súmula e o jogo ficam <b>Encerrados</b> com o placar <b>{casa.placarReconstruido} × {visitante.placarReconstruido}</b>.</li>
              <li>A <b>classificação</b> da temporada é recalculada.</li>
              <li>A relação dos jogadores <b>não muda</b>.</li>
            </ul>
            {errosImportacao.length > 0 && (
              <div className={base.erroGlobal} style={{ marginTop: 10 }}>
                {errosImportacao.map(m => <p key={m} style={{ margin: '2px 0' }}>{m}</p>)}
              </div>
            )}
            <div className={base.modalFooter}>
              <button className={base.btnSecondary} onClick={() => setConfirmando(false)} disabled={importando}>Cancelar</button>
              <button className={`${base.btnPrimary} ${base.btnPerigoSolido}`} onClick={importar} disabled={importando}>
                {importando ? 'Importando...' : 'Substituir e encerrar o jogo'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

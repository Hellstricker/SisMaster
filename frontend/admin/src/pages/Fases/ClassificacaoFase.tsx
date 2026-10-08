import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import type { StatusFase, TipoFase } from '../../api/fases';
import {
  type ClassificacaoFase as Dados,
  type ConfrontoClassificacao,
  type LinhaClassificacao,
  type StatusJogo,
  definirSorteio,
  obterClassificacaoFase,
} from '../../api/jogos';
import base from './Fases.module.css';
import styles from './Classificacao.module.css';

const TIPO_ROTULO: Record<TipoFase, string> = { PontosCorridos: 'Pontos corridos', Grupos: 'Grupos', MataMata: 'Mata-mata' };
const STATUS_ROTULO: Record<StatusFase, string> = { Planejada: 'Planejada', EmAndamento: 'Em andamento', Encerrada: 'Encerrada' };
const STATUS_ESTILO: Record<StatusFase, string> = { Planejada: base.stPlanejada, EmAndamento: base.stEmAndamento, Encerrada: base.stEncerrada };

const numero = (n: number) => n.toLocaleString('pt-BR', { maximumFractionDigits: 2 });

function textoClassificados(d: Dados): string {
  if (d.tipo === 'MataMata') return 'avançam os vencedores';
  if (d.classificadosPrimeiros == null) return 'classificam todos';
  const por = d.tipo === 'Grupos' ? ' de cada grupo' : '';
  const base = d.classificadosPrimeiros === 1 ? `classifica o 1º colocado${por}` : `classificam os ${d.classificadosPrimeiros} primeiros${por}`;
  return d.melhoresExtras > 0 ? `${base} + ${d.melhoresExtras} melhor(es) ${d.classificadosPrimeiros + 1}º colocado(s)` : base;
}

function statusJogoRotulo(status: StatusJogo): string {
  return ({ Agendado: 'agendado', EmAndamento: 'em andamento', Encerrado: 'encerrado', WO: 'W.O.', Dispensado: 'dispensado' } as const)[status];
}

/** Classificação da fase (Tela 12): calculada dos jogos encerrados; tabela, grupos ou séries de mata-mata. */
export default function ClassificacaoFase() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [dados, setDados] = useState<Dados | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');

  const [sorteio, setSorteio] = useState<{ grupoOrdem: number | null; linhas: LinhaClassificacao[] } | null>(null);

  const carregar = useCallback(() => {
    if (!id) return Promise.resolve();
    return obterClassificacaoFase(id)
      .then(setDados)
      .catch(e => setErro(e instanceof Error ? e.message : 'Não foi possível carregar a classificação.'))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;
  if (erro || !dados) return <div className={base.page}><p className={base.erroGlobal}>{erro || 'Fase não encontrada.'}</p></div>;

  const corpo = dados.classificacao;

  return (
    <div className={base.page}>
      <div className={base.topbar}>
        <button className={base.btnVoltar} onClick={() => navigate(`/fases/${dados.id}`)}>‹ Voltar ao detalhe da fase</button>
      </div>

      <h1 className={base.titulo}>Classificação — {dados.nome} · {dados.categoria.nome}</h1>
      <p className={base.sub}>Calculada na hora a partir dos jogos encerrados (e W.O.); nada é gravado.</p>

      <div className={base.chips}>
        <span className={base.chip}>Tipo: <b>{TIPO_ROTULO[dados.tipo]}</b></span>
        <span className={base.chip}>Jogos encerrados: <b>{dados.jogosEncerrados} de {dados.totalJogos}</b></span>
        <span className={base.chip}>{textoClassificados(dados)}</span>
        <span className={`${base.status} ${STATUS_ESTILO[dados.status]}`}>{STATUS_ROTULO[dados.status]}</span>
      </div>

      {!corpo && (
        <div className={`${base.banner} ${base.bannerAviso}`}>
          <span><b>A tabela de jogos ainda não foi gerada.</b> A classificação aparece quando houver jogos.</span>
        </div>
      )}

      {corpo && corpo.tipo !== 'MataMata' && (
        <div className={`${base.banner} ${dados.bonificacaoAtiva ? base.bannerAberto : base.bannerAviso}`}>
          <span>
            {dados.bonificacaoAtiva
              ? <><b>Bonificação ativa:</b> {numero(dados.temporada.valorBonificacaoPorAtleta)} ponto(s) por atleta que cumpriu o rodízio (só jogos encerrados com súmula; W.O. não bonifica).</>
              : <><b>Bonificação desligada nesta temporada.</b> O total usa só vitórias, derrotas e W.O.</>}
          </span>
        </div>
      )}

      {corpo?.tipo === 'Tabela' && (
        <div className={styles.card}>
          <h2 className={styles.cardTitulo}>Tabela {!corpo.definitiva && <span className={`${base.status} ${base.stEmAndamento}`}>parcial</span>}</h2>
          <AvisoSorteio podeDefinir={corpo.podeDefinirSorteio} definido={corpo.sorteioDefinido} linhas={corpo.linhas} onAbrir={() => setSorteio({ grupoOrdem: null, linhas: corpo.linhas.filter(l => l.empatePorSorteio) })} />
          <Tabela linhas={corpo.linhas} />
          <Legenda temMelhor={false} definitiva={corpo.definitiva} />
        </div>
      )}

      {corpo?.tipo === 'Grupos' && (
        <>
          <div className={styles.grupos}>
            {corpo.grupos.map(g => (
              <div key={g.ordem} className={styles.card}>
                <h2 className={styles.cardTitulo}>
                  Grupo {g.nome} {!g.definitiva && <span className={`${base.status} ${base.stEmAndamento}`}>parcial</span>}
                </h2>
                <AvisoSorteio podeDefinir={g.podeDefinirSorteio} definido={g.sorteioDefinido} linhas={g.linhas} onAbrir={() => setSorteio({ grupoOrdem: g.ordem, linhas: g.linhas.filter(l => l.empatePorSorteio) })} />
                <Tabela linhas={g.linhas} />
              </div>
            ))}
          </div>
          {corpo.melhores.length > 0 && (
            <div className={styles.card}>
              <h2 className={styles.cardTitulo}>
                Melhores {(dados.classificadosPrimeiros ?? 0) + 1}º colocados
                <span className={base.chip}>classificam {dados.melhoresExtras}</span>
              </h2>
              <div className={styles.scroll}>
                <table className={styles.tabela}>
                  <thead><tr><th>#</th><th className={styles.esq}>Equipe</th><th>Grupo</th><th>Pontos</th><th>Saldo</th><th>PF</th></tr></thead>
                  <tbody>
                    {corpo.melhores.map(m => (
                      <tr key={m.linha.vagaId} className={m.linha.classificado === 'Melhor' ? styles.melhor : ''}>
                        <td className={styles.pos}>{m.posicao}º</td>
                        <td className={styles.esq}><Nome l={m.linha} /></td>
                        <td>{m.grupo}</td>
                        <td className={styles.total}>{numero(m.linha.total)}</td>
                        <td>{sinal(m.linha.saldo)}</td>
                        <td>{m.linha.pontosFeitos}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
          <Legenda temMelhor={corpo.melhores.length > 0} definitiva={corpo.definitiva} />
        </>
      )}

      {sorteio && (
        <SorteioModal
          linhas={sorteio.linhas}
          onFechar={() => setSorteio(null)}
          onSalvar={async ordem => {
            await definirSorteio(dados.id, sorteio.grupoOrdem, ordem);
            setSorteio(null);
            await carregar();
          }}
        />
      )}

      {corpo?.tipo === 'MataMata' && (
        <div className={styles.card}>
          <h2 className={styles.cardTitulo}>Confrontos <span className={base.chip}>melhor de {dados.jogosPorConfronto} · vence quem chegar a {corpo.vitoriasNecessarias}</span></h2>
          <div className={styles.confrontos}>
            {corpo.confrontos.map(c => <Confronto key={c.id} c={c} />)}
            {corpo.confrontos.length === 0 && <p className={base.info}>Nenhum confronto cadastrado.</p>}
          </div>
        </div>
      )}
    </div>
  );
}

const sinal = (n: number) => (n > 0 ? `+${n}` : String(n));

function Nome({ l }: { l: LinhaClassificacao }) {
  return (
    <span className={l.equipe ? undefined : styles.referencia}>
      {l.equipe?.cor && <i className={styles.cor} style={{ background: l.equipe.cor }} />}
      {l.texto}
      {l.empatePorSorteio && <span className={styles.sorteio} title="Empate que nenhum critério resolveu: a diretoria decide por sorteio">sorteio</span>}
    </span>
  );
}

function Tabela({ linhas }: { linhas: LinhaClassificacao[] }) {
  return (
    <div className={styles.scroll}>
      <table className={styles.tabela}>
        <thead>
          <tr>
            <th rowSpan={2}>#</th><th rowSpan={2} className={styles.esq}>Equipe</th>
            <th colSpan={4}>Jogos</th><th colSpan={3}>Pontos por resultado</th>
            <th rowSpan={2}>Subtotal</th><th rowSpan={2}>Bonificação</th><th rowSpan={2}>Total</th>
            <th colSpan={3}>Cestas</th><th rowSpan={2}>Últimos</th>
          </tr>
          <tr>
            <th>J</th><th>V</th><th>D</th><th>W.O.</th>
            <th className={styles.pontos}>V ×2</th><th className={styles.pontos}>D ×1</th><th className={styles.pontos}>W.O. ×0</th>
            <th>PF</th><th>PS</th><th>Saldo</th>
          </tr>
        </thead>
        <tbody>
          {linhas.map(l => (
            <tr key={l.vagaId} className={l.classificado === 'Direto' ? styles.direto : l.classificado === 'Melhor' ? styles.melhor : ''}>
              <td className={styles.pos}>{l.posicao}º</td>
              <td className={styles.esq}><Nome l={l} /></td>
              <td>{l.jogos}</td><td>{l.vitorias}</td><td>{l.derrotas}</td><td>{l.derrotasWO}</td>
              <td className={styles.pontos}>{l.pontosPorVitorias}</td>
              <td className={styles.pontos}>{l.pontosPorDerrotas}</td>
              <td className={styles.pontos}>{l.pontosPorWO}</td>
              <td><b>{l.subtotal}</b></td>
              <td>{l.bonificacao > 0 ? `+${numero(l.bonificacao)}` : <span className={styles.zero}>0</span>}</td>
              <td className={styles.total}>{numero(l.total)}</td>
              <td>{l.pontosFeitos}</td><td>{l.pontosSofridos}</td><td>{sinal(l.saldo)}</td>
              <td>
                <span className={styles.seq}>
                  {l.sequencia.split('').map((c, i) => <i key={i} className={c === 'V' ? styles.seqV : styles.seqD}>{c}</i>)}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Legenda({ temMelhor, definitiva }: { temMelhor: boolean; definitiva: boolean }) {
  return (
    <div className={styles.legenda}>
      <span className={styles.lgDireto}>{definitiva ? 'classificado' : 'na zona de classificação (ainda não definitivo)'}</span>
      {temMelhor && <span className={styles.lgMelhor}>classificado como melhor colocado</span>}
      <span>Pontos: vitória 2, derrota 1, W.O. 0. Total = subtotal + bonificação.</span>
    </div>
  );
}

function Confronto({ c }: { c: ConfrontoClassificacao }) {
  const lado = (l: ConfrontoClassificacao['a']) => (
    <div className={`${styles.lado} ${l.venceu ? styles.venceu : ''}`}>
      <span className={l.equipe ? undefined : styles.referencia}>
        {l.equipe?.cor && <i className={styles.cor} style={{ background: l.equipe.cor }} />}
        {l.texto}{l.venceu ? ' ✔' : ''}
      </span>
      <b className={styles.placarSerie}>{l.vitorias}</b>
    </div>
  );
  return (
    <div className={styles.confronto}>
      <div className={styles.confrontoTopo}>
        <span>{c.nome}</span>
        <span className={`${base.status} ${c.decidido ? base.stEncerrada : c.iniciado ? base.stEmAndamento : base.stPlanejada}`}>
          {c.decidido ? 'série decidida' : c.iniciado ? 'em andamento' : 'aguardando'}
        </span>
      </div>
      {lado(c.a)}
      {lado(c.b)}
      <div className={styles.jogosSerie}>
        {c.jogos.map(j => {
          const resultado = j.placarCasa != null && j.placarVisitante != null ? `${j.placarCasa}×${j.placarVisitante}` : statusJogoRotulo(j.status);
          return (
            <span key={j.id} className={`${styles.chipJogo} ${j.opcional ? styles.opcional : ''} ${j.status === 'Dispensado' ? styles.dispensado : ''}`}>
              J{j.jogoDaSerie ?? '?'}{j.opcional ? ' (se necessário)' : ''} · #{j.numero} · {resultado}
            </span>
          );
        })}
      </div>
    </div>
  );
}

function AvisoSorteio({ podeDefinir, definido, linhas, onAbrir }: { podeDefinir: boolean; definido: boolean; linhas: LinhaClassificacao[]; onAbrir: () => void }) {
  if (!podeDefinir && !definido) return null;
  const nomes = linhas.filter(l => l.empatePorSorteio).map(l => l.texto).join(', ');
  return (
    <div className={`${base.banner} ${definido ? base.bannerAberto : base.bannerAviso}`}>
      <span>
        {definido
          ? <><b>Sorteio registrado</b> para o empate entre {nomes}. A ordem abaixo segue o sorteio.</>
          : <><b>Empate sem critério</b> entre {nomes}. Faça o sorteio e registre a ordem para as próximas fases serem definidas.</>}
      </span>
      {podeDefinir && <button className={base.btnSecondary} onClick={onAbrir}>{definido ? 'Alterar sorteio' : 'Registrar sorteio'}</button>}
    </div>
  );
}

/** Ordena as equipes empatadas conforme o sorteio da diretoria (da melhor para a pior colocação). */
function SorteioModal({ linhas, onSalvar, onFechar }: { linhas: LinhaClassificacao[]; onSalvar: (ordem: string[]) => Promise<void>; onFechar: () => void }) {
  const [ordem, setOrdem] = useState(linhas);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const mover = (i: number, d: -1 | 1) => setOrdem(p => {
    const j = i + d;
    if (j < 0 || j >= p.length) return p;
    const n = [...p];
    [n[i], n[j]] = [n[j], n[i]];
    return n;
  });

  async function salvar() {
    setSalvando(true);
    try {
      await onSalvar(ordem.map(l => l.vagaId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao registrar o sorteio.');
      setSalvando(false);
    }
  }

  return (
    <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={base.modal}>
        <h2 className={base.modalTitulo}>Sorteio de desempate</h2>
        <p className={base.dica}>Coloque as equipes na ordem sorteada: a primeira fica com a melhor colocação entre elas.</p>
        {ordem.map((l, i) => (
          <div key={l.vagaId} className={styles.sorteioLinha}>
            <b>{i + 1}º</b>
            <span>{l.equipe?.cor && <i className={styles.cor} style={{ background: l.equipe.cor }} />}{l.texto}</span>
            <span className={styles.sorteioAcoes}>
              <button className={base.btnSecondary} disabled={i === 0} onClick={() => mover(i, -1)} aria-label="Subir">↑</button>
              <button className={base.btnSecondary} disabled={i === ordem.length - 1} onClick={() => mover(i, 1)} aria-label="Descer">↓</button>
            </span>
          </div>
        ))}
        {erro && <p className={base.erroGlobal}>{erro}</p>}
        <div className={base.modalFooter}>
          <button className={base.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={base.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Registrar sorteio'}</button>
        </div>
      </div>
    </div>
  );
}

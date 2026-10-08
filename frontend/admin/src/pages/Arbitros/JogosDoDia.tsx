import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { type JogoDoArbitro, listarJogosDoArbitro } from '../../api/arbitros';
import { linksDaSumula } from '../../api/jogos';
import EncerrarSumula from '../Sumula/EncerrarSumula';
import base from '../Fases/Fases.module.css';
import styles from './Arbitros.module.css';

const hojeIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const dataBr = (iso: string) => iso.split('-').reverse().join('/');
const dataLonga = (iso: string) =>
  new Date(`${iso}T12:00:00`).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' });
const horaCurta = (h: string | null) => (h ? h.slice(0, 5) : '');
const rotuloLocal = (l: NonNullable<JogoDoArbitro['local']>) => `${l.nome} — ${l.cidade}${l.estado ? `/${l.estado}` : ''}`;

const SUMULA_ROTULO = { SemSumula: 'sem súmula', EmPreparacao: 'súmula em preparação', EmAndamento: 'em andamento', Intervalo: 'em andamento', Encerrada: 'encerrada' } as const;

/** Primeira tela do árbitro: jogos de hoje; antes, os pendentes de dias anteriores; depois, os próximos dias. */
export default function JogosDoDia() {
  const [jogos, setJogos] = useState<JogoDoArbitro[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [fData, setFData] = useState('');
  const [fCategoria, setFCategoria] = useState('');
  const [fLocal, setFLocal] = useState('');
  const [aviso, setAviso] = useState('');
  const [hoje, setHoje] = useState(hojeIso());

  const carregar = useCallback(async () => {
    try {
      setErro('');
      setJogos((await listarJogosDoArbitro()).jogos);
      setHoje(hojeIso());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os jogos.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    carregar();
    const t = setInterval(carregar, 30000);
    return () => clearInterval(t);
  }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(''), 4000);
    return () => clearTimeout(t);
  }, [aviso]);

  const datas = useMemo(() => [...new Set(jogos.map(j => j.data))].sort(), [jogos]);
  const categorias = useMemo(() => [...new Set(jogos.map(j => j.categoria.nome))].sort(), [jogos]);
  const locais = useMemo(() => [...new Map(jogos.filter(j => j.local).map(j => [j.local!.id, rotuloLocal(j.local!)])).entries()], [jogos]);

  const filtrados = jogos.filter(j =>
    (!fData || j.data === fData) && (!fCategoria || j.categoria.nome === fCategoria) && (!fLocal || j.local?.id === fLocal));
  const atrasados = filtrados.filter(j => j.data < hoje);
  const doDia = filtrados.filter(j => j.data === hoje);
  const proximos = filtrados.filter(j => j.data > hoje);

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;

  return (
    <div className={`${base.page} ${styles.pagina}`}>
      <h1 className={base.titulo}>Jogos do dia</h1>
      <p className={base.sub}>{dataLonga(hoje)} — escolha o jogo para preparar a súmula, iniciá-la e abrir as telas da mesa.</p>

      {erro && <p className={base.erroGlobal}>{erro}</p>}

      <div className={styles.topo}>
        <span className={base.info}>{doDia.length} jogo(s) hoje{proximos.length > 0 ? ` · ${proximos.length} nos próximos dias` : ''}</span>
        <button className={`${base.btnSecondary} ${base.btnSm}`} onClick={carregar}>↻ Atualizar</button>
      </div>

      <div className={styles.filtros}>
        <select className={base.select} value={fData} onChange={e => setFData(e.target.value)} aria-label="Filtrar por data">
          <option value="">Todas as datas</option>
          {datas.map(d => <option key={d} value={d}>{dataBr(d)}</option>)}
        </select>
        <select className={base.select} value={fCategoria} onChange={e => setFCategoria(e.target.value)} aria-label="Filtrar por categoria">
          <option value="">Todas as categorias</option>
          {categorias.map(c => <option key={c} value={c}>{c}</option>)}
        </select>
        <select className={base.select} value={fLocal} onChange={e => setFLocal(e.target.value)} aria-label="Filtrar por local">
          <option value="">Todos os locais</option>
          {locais.map(([id, nome]) => <option key={id} value={id}>{nome}</option>)}
        </select>
      </div>

      {atrasados.length > 0 && (
        <section>
          <h2 className={`${styles.dia} ${styles.atencao}`}>⚠ Pendentes de dias anteriores <small>agendados e ainda não encerrados</small></h2>
          {atrasados.map(j => <Cartao key={j.id} j={j} mostrarData onMudou={async () => { setAviso('Atualizado.'); await carregar(); }} />)}
        </section>
      )}

      <section>
        <h2 className={styles.dia}>Hoje <small>{dataBr(hoje)}</small></h2>
        {doDia.length === 0
          ? <p className={styles.vazio}>Nenhum jogo agendado para hoje.</p>
          : <PorLocal lista={doDia} onMudou={async () => { setAviso('Atualizado.'); await carregar(); }} />}
      </section>

      {proximos.length > 0 && (
        <section>
          <h2 className={styles.dia}>Próximos dias</h2>
          {[...new Set(proximos.map(j => j.data))].map(d => (
            <div key={d}>
              <div className={styles.diaSub}>{dataLonga(d)}</div>
              <PorLocal lista={proximos.filter(j => j.data === d)} onMudou={async () => { setAviso('Atualizado.'); await carregar(); }} />
            </div>
          ))}
        </section>
      )}

      {jogos.length === 0 && !erro && <p className={styles.vazio}>Nenhum jogo agendado. A gestão define data, horário e local dos jogos.</p>}
      {aviso && <div className={base.toast} role="status">{aviso}</div>}
    </div>
  );
}

function PorLocal({ lista, onMudou }: { lista: JogoDoArbitro[]; onMudou: () => void | Promise<void> }) {
  let atual = '';
  return (
    <>
      {lista.map(j => {
        const nome = j.local ? rotuloLocal(j.local) : 'Local a definir';
        const cabecalho = nome !== atual;
        atual = nome;
        return (
          <div key={j.id}>
            {cabecalho && <div className={styles.local}>📍 {nome}</div>}
            <Cartao j={j} onMudou={onMudou} />
          </div>
        );
      })}
    </>
  );
}

function Cartao({ j, mostrarData = false, onMudou }: { j: JogoDoArbitro; mostrarData?: boolean; onMudou: () => void | Promise<void> }) {
  const sumulaStatus = j.sumula?.status ?? 'SemSumula';
  const emJogo = j.sumula && j.sumula.status !== 'EmPreparacao' && j.sumula.status !== 'Encerrada';
  const links = j.sumula ? linksDaSumula(j.sumula.id) : null;
  const lado = (x: JogoDoArbitro['casa']) => <span className={x.definida ? undefined : styles.ref}>{x.texto}</span>;

  return (
    <div className={styles.jogo}>
      <div>
        <div className={styles.hora}>{horaCurta(j.hora) || '—'}</div>
        <div className={styles.num}>#{j.numero}{mostrarData ? ` · ${dataBr(j.data)}` : ''}</div>
      </div>
      <div>
        <div className={styles.par}>{lado(j.casa)}<span className={styles.x}>×</span>{lado(j.visitante)}</div>
        <div className={styles.meta}>
          <span className={styles.tag}>{j.categoria.nome}</span>
          <span>{j.fase.nome} · {j.etiqueta}</span>
          {j.podePrepararSumula && <span className={`${styles.pill} ${emJogo ? styles.pillInfo : sumulaStatus === 'EmPreparacao' ? styles.pillOk : ''}`}>{SUMULA_ROTULO[sumulaStatus]}</span>}
        </div>
      </div>
      <div className={styles.acoes}>
        {!j.podePrepararSumula ? (
          <span className={`${styles.pill} ${styles.pillAviso}`}>aguardando equipes</span>
        ) : !j.sumula ? (
          <Link className={`${base.btnPrimary} ${styles.botao}`} to={`/jogos/${j.id}/sumula`}>Preparar súmula ›</Link>
        ) : j.sumula.status === 'EmPreparacao' ? (
          <Link className={`${base.btnPrimary} ${styles.botao}`} to={`/jogos/${j.id}/sumula`}>Continuar preparação ›</Link>
        ) : (
          <>
            <div className={styles.links}>
              <a className={`${base.btnSecondary} ${base.btnSm} ${styles.botao}`} href={links!.mesarioTempo} target="_blank" rel="noreferrer">Mesário — tempo</a>
              <a className={`${base.btnSecondary} ${base.btnSm} ${styles.botao}`} href={links!.mesarioStats} target="_blank" rel="noreferrer">Mesário — estatísticas</a>
              <a className={`${base.btnSecondary} ${base.btnSm} ${styles.botao}`} href={links!.placar} target="_blank" rel="noreferrer">Placar</a>
            </div>
            <EncerrarSumula pequeno sumulaId={j.sumula.id} jogo={`#${j.numero} — ${j.casa.texto} × ${j.visitante.texto}`} onEncerrada={onMudou} />
          </>
        )}
      </div>
    </div>
  );
}

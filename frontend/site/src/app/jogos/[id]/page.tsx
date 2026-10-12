import Link from 'next/link';
import { notFound } from 'next/navigation';
import AtualizarSozinho from '@/components/AtualizarSozinho';
import { type LadoDaSumula, dataBr, obterJogo, relogio, rotuloPeriodo } from '@/lib/api';

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }) {
  const r = await obterJogo((await params).id);
  return { title: r ? `${r.jogo.casa.texto} × ${r.jogo.visitante.texto}` : 'Jogo' };
}

function Estatisticas({ lado, classe }: { lado: LadoDaSumula; classe: string }) {
  return (
    <div className="card" style={{ margin: 0 }}>
      <h3 className={classe}>{lado.nome}</h3>
      <div className="rolagem">
        <table>
          <thead><tr><th className="l">#</th><th className="l">Atleta</th><th>PTS</th><th>REB</th><th>AST</th><th>ROU</th><th>TOC</th><th>F</th></tr></thead>
          <tbody>
            {lado.jogadores.map(j => (
              <tr key={`${j.numero}-${j.nome}`}>
                <td className="l">{j.numero}</td><td className="l">{j.nome}</td>
                <td><b>{j.pontos}</b></td><td>{j.rebotes}</td><td>{j.assistencias}</td><td>{j.roubadas}</td><td>{j.tocos}</td><td>{j.faltas}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {lado.tecnico && <p className="small muted" style={{ margin: '8px 0 0' }}>Técnico: {lado.tecnico}</p>}
    </div>
  );
}

export default async function Jogo({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const r = await obterJogo(id);
  if (!r) notFound();
  const { jogo: j, sumula } = r;
  const vivo = j.status === 'EmAndamento';
  const encerrado = j.status === 'Encerrado' || j.status === 'WO';
  const pc = vivo ? j.aoVivo?.placarCasa ?? j.placarCasa : j.placarCasa;
  const pv = vivo ? j.aoVivo?.placarVisitante ?? j.placarVisitante : j.placarVisitante;

  return (
    <main className="wrap">
      {vivo && <AtualizarSozinho segundos={5} />}
      <p className="small muted" style={{ margin: '0 0 6px' }}>
        <Link href="/">Início</Link> › <Link href={`/campeonatos/${j.temporada.id}`}>{r.campeonato.nome}</Link> › Jogo #{j.numero}
      </p>
      <div className="card">
        <div className="row between">
          <span>
            {vivo && <span className="badge live">● AO VIVO{j.aoVivo ? ` · ${rotuloPeriodo(j.aoVivo.periodo)} · ${relogio(j.aoVivo.segundosRestantes)}` : ''}</span>}
            {j.status === 'Encerrado' && <span className="badge ok">Encerrado</span>}
            {j.status === 'WO' && <span className="badge wo">W.O.</span>}
            {j.status === 'Agendado' && <span className="badge">Agendado</span>}
          </span>
          <span className="muted small">
            {j.categoria} · {j.fase} · {j.etiqueta}
            {j.data ? ` · ${dataBr(j.data)}` : ''}{j.hora ? ` ${j.hora}` : ''}
            {j.local ? ` · 📍 ${j.local.nome}, ${j.local.cidade}${j.local.estado ? `/${j.local.estado}` : ''}` : ''}
          </span>
        </div>
        <div className="grande">
          <div className="nome casa">{j.casa.texto}</div>
          <div className="n">{vivo || encerrado ? `${pc ?? 0} × ${pv ?? 0}` : j.hora ?? '× '}</div>
          <div className="nome visit">{j.visitante.texto}</div>
        </div>
        {sumula && sumula.parciais.length > 0 && (
          <div className="rolagem">
            <table className="parciais">
              <thead><tr><th className="l" />{sumula.parciais.map(p => <th key={p.periodo}>{p.periodo >= 5 ? 'Pr' : `${p.periodo}º`}</th>)}<th>Total</th></tr></thead>
              <tbody>
                <tr><td className="l casa"><b>{sumula.casa.nome}</b></td>{sumula.parciais.map(p => <td key={p.periodo}>{p.casa}</td>)}<td><b>{sumula.placarCasa}</b></td></tr>
                <tr><td className="l visit"><b>{sumula.visitante.nome}</b></td>{sumula.parciais.map(p => <td key={p.periodo}>{p.visitante}</td>)}<td><b>{sumula.placarVisitante}</b></td></tr>
              </tbody>
            </table>
          </div>
        )}
        {vivo && j.aoVivo && (
          <div className="row" style={{ justifyContent: 'center', marginTop: 12 }}>
            <a className="btn primary" href={`/arbitros/placar.html?partidaId=${j.aoVivo.sumulaId}`}>Abrir placar</a>
          </div>
        )}
      </div>

      {sumula ? (
        <>
          <h2 style={{ marginTop: 18 }}>Súmula</h2>
          <div className="grid g2">
            <Estatisticas lado={sumula.casa} classe="casa" />
            <Estatisticas lado={sumula.visitante} classe="visit" />
          </div>
        </>
      ) : (
        <p className="muted" style={{ marginTop: 18 }}>A súmula aparece aqui quando o jogo começar.</p>
      )}
    </main>
  );
}

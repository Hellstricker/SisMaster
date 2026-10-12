import Link from 'next/link';
import { notFound } from 'next/navigation';
import AtualizarSozinho from '@/components/AtualizarSozinho';
import Classificacao from '@/components/Classificacao';
import { type CartaoDeJogo, diaDaSemana, diaMes, obterClassificacao, obterTemporada } from '@/lib/api';

type Aba = 'jogos' | 'classificacao' | 'equipes';
type Busca = { cat?: string; aba?: string; fase?: string; sit?: string };

const STATUS: Record<string, string> = {
  InscricoesAbertas: 'Inscrições abertas', InscricoesEncerradas: 'Inscrições encerradas', EmAndamento: 'Em andamento', Encerrada: 'Encerrado',
};

function href(id: string, b: Busca) {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(b)) if (v) q.set(k, v);
  const s = q.toString();
  return `/campeonatos/${id}${s ? `?${s}` : ''}`;
}

function Placar({ j }: { j: CartaoDeJogo }) {
  if (j.status === 'WO') return <><span className="badge wo">W.O.</span> {j.placarCasa} × {j.placarVisitante}</>;
  if (j.status === 'Encerrado') return <b>{j.placarCasa} × {j.placarVisitante}</b>;
  if (j.status === 'EmAndamento') return <span className="badge live">● {j.aoVivo?.placarCasa ?? 0} × {j.aoVivo?.placarVisitante ?? 0}</span>;
  return <span className="muted">{j.hora ?? 'a jogar'}</span>;
}

export async function generateMetadata({ params }: { params: Promise<{ id: string }> }) {
  const t = await obterTemporada((await params).id);
  return { title: t ? `${t.campeonato} ${t.ano}` : 'Campeonato' };
}

export default async function Temporada({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<Busca> }) {
  const { id } = await params;
  const busca = await searchParams;
  const t = await obterTemporada(id);
  if (!t) notFound();

  const categoria = t.categorias.find(c => c.id === busca.cat) ?? t.categorias[0];
  const aba: Aba = busca.aba === 'classificacao' || busca.aba === 'equipes' ? busca.aba : 'jogos';
  const fasesDaCategoria = t.fases.filter(f => f.categoriaId === categoria?.id);
  const fase = fasesDaCategoria.find(f => f.id === busca.fase) ?? fasesDaCategoria[0];
  const sit = busca.sit === 'encerrados' || busca.sit === 'proximos' ? busca.sit : 'todos';
  const temAoVivo = t.jogos.some(j => j.status === 'EmAndamento');

  let jogos = t.jogos.filter(j => j.categoria === categoria?.nome && (!fase || j.fase === fase.nome));
  if (sit === 'encerrados') jogos = jogos.filter(j => j.status === 'Encerrado' || j.status === 'WO');
  if (sit === 'proximos') jogos = jogos.filter(j => j.status === 'Agendado' || j.status === 'EmAndamento');

  const classificacao = aba === 'classificacao' && fase ? await obterClassificacao(fase.id) : null;
  const equipes = t.equipes.filter(e => e.categoriaId === categoria?.id);
  const base = { cat: categoria?.id, aba: aba === 'jogos' ? undefined : aba, fase: fase?.id };

  return (
    <main className="wrap">
      <AtualizarSozinho segundos={temAoVivo ? 5 : 30} />
      <p className="small muted" style={{ margin: '0 0 6px' }}><Link href="/campeonatos">Campeonatos</Link> › {t.campeonato}</p>
      <h1>{t.campeonato} <span className="muted">· {t.ano}</span></h1>
      <p className="sub"><span className="badge">{STATUS[t.status] ?? t.status}</span> &nbsp;{t.categorias.length} {t.categorias.length === 1 ? 'categoria' : 'categorias'} · {t.totalEquipes} equipes</p>

      {t.categorias.length > 1 && (
        <nav className="chips" aria-label="Categoria">
          {t.categorias.map(c => (
            <Link key={c.id} href={href(id, { cat: c.id, aba: aba === 'jogos' ? undefined : aba })} className={c.id === categoria?.id ? 'on' : ''} aria-current={c.id === categoria?.id ? 'true' : undefined}>{c.nome}</Link>
          ))}
        </nav>
      )}

      <nav className="abas" aria-label="Seções">
        {(['jogos', 'classificacao', 'equipes'] as const).map(a => (
          <Link key={a} href={href(id, { cat: categoria?.id, aba: a === 'jogos' ? undefined : a, fase: fase?.id })} className={aba === a ? 'on' : ''} aria-current={aba === a ? 'page' : undefined}>
            {a === 'jogos' ? 'Jogos' : a === 'classificacao' ? 'Classificação' : 'Equipes'}
          </Link>
        ))}
      </nav>

      {!t.tabelaGerada && aba !== 'equipes' && <p className="muted">A tabela de jogos ainda não foi divulgada.</p>}

      {fasesDaCategoria.length > 1 && aba !== 'equipes' && (
        <nav className="chips" aria-label="Fase">
          {fasesDaCategoria.map(f => (
            <Link key={f.id} href={href(id, { ...base, fase: f.id })} className={f.id === fase?.id ? 'on' : ''}>{f.nome}</Link>
          ))}
        </nav>
      )}

      {aba === 'jogos' && t.tabelaGerada && (
        <>
          <nav className="chips" aria-label="Situação">
            {([['todos', 'Todos'], ['encerrados', 'Encerrados'], ['proximos', 'Próximos']] as const).map(([k, r]) => (
              <Link key={k} href={href(id, { ...base, sit: k === 'todos' ? undefined : k })} className={sit === k ? 'on' : ''}>{r}</Link>
            ))}
          </nav>
          {jogos.length === 0 ? <p className="muted">Nenhum jogo nesta seleção.</p> : (
            <div className="card rolagem">
              <table>
                <thead><tr><th className="l">Data</th><th className="l">Rodada</th><th className="l">Jogo</th><th>Placar</th><th className="l">Local</th></tr></thead>
                <tbody>
                  {jogos.map(j => (
                    <tr key={j.id}>
                      <td className="l">{j.data ? `${diaDaSemana(j.data)} ${diaMes(j.data)}${j.hora ? ` · ${j.hora}` : ''}` : <span className="muted">a definir</span>}</td>
                      <td className="l">{j.etiqueta}</td>
                      <td className="l"><Link href={`/jogos/${j.id}`}>{j.casa.texto} × {j.visitante.texto}</Link></td>
                      <td><Placar j={j} /></td>
                      <td className="l muted">{j.local?.nome ?? ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {aba === 'classificacao' && t.tabelaGerada && (
        classificacao ? <Classificacao dados={classificacao} /> : <p className="aviso" role="status">Não foi possível carregar a classificação agora.</p>
      )}

      {aba === 'equipes' && (
        equipes.length === 0 ? <p className="muted">Ainda não há equipes nesta categoria.</p> : (
          <>
            <div className="grid g3">
              {equipes.map(e => (
                <div className="card" key={e.id} style={{ margin: 0 }}>
                  <h3>{e.nome}</h3>
                  <p className="muted small" style={{ margin: 0 }}>{e.categoria} · {e.atletas} {e.atletas === 1 ? 'atleta' : 'atletas'}</p>
                </div>
              ))}
            </div>
            <p className="small muted" style={{ marginTop: 10 }}>Os nomes dos atletas aparecem na súmula de cada jogo.</p>
          </>
        )
      )}
    </main>
  );
}

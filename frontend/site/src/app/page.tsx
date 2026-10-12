import Link from 'next/link';
import AtualizarSozinho from '@/components/AtualizarSozinho';
import CartaoJogo from '@/components/CartaoDeJogo';
import { dataBr, diaDaSemana, diaMes, obterAssociacao, obterInicio, obterInscricoesAbertas } from '@/lib/api';

export default async function Inicio() {
  const [associacao, abertas, inicio] = await Promise.all([obterAssociacao(), obterInscricoesAbertas(), obterInicio()]);
  const temAoVivo = !!inicio && inicio.aoVivo.length > 0;

  return (
    <main className="wrap">
      {/* Ao vivo: 5 s. Com jogo marcado para hoje (pode começar a qualquer momento): 10 s. Senão 30 s. */}
      <AtualizarSozinho segundos={temAoVivo ? 5 : inicio && inicio.jogosDeHoje.length > 0 ? 10 : 30} />
      <h1>{associacao ? associacao.nome : 'Basquete Master'}</h1>
      <p className="sub">Inscrições, jogos e resultados.</p>

      {inicio === null && (
        <p className="aviso" role="status">Não foi possível carregar os jogos agora. Tente de novo em instantes.</p>
      )}

      {inicio && inicio.aoVivo.length > 0 && (
        <section aria-labelledby="vivo" className="bloco">
          <h2 id="vivo">Ao vivo agora <span className="badge live">● AO VIVO</span></h2>
          <div className="grid g2">{inicio.aoVivo.map(j => <CartaoJogo key={j.id} j={j} />)}</div>
        </section>
      )}

      {inicio && (
        <section aria-labelledby="hoje" className="bloco">
          <h2 id="hoje">Jogos de hoje <span className="muted small" style={{ fontWeight: 400 }}>· {diaDaSemana(inicio.hoje)}, {dataBr(inicio.hoje)}</span></h2>
          {inicio.jogosDeHoje.length === 0
            ? <p className="muted">{inicio.aoVivo.length > 0 ? 'Nenhum outro jogo agendado para hoje.' : 'Nenhum jogo hoje. Veja os próximos jogos abaixo.'}</p>
            : <div className="grid g2">{inicio.jogosDeHoje.map(j => <CartaoJogo key={j.id} j={j} />)}</div>}
        </section>
      )}

      {inicio && inicio.resultados.length > 0 && (
        <section aria-labelledby="res" className="bloco">
          <h2 id="res">Últimos resultados</h2>
          <div className="grid g2">{inicio.resultados.map(j => <CartaoJogo key={j.id} j={j} mostrarData />)}</div>
        </section>
      )}

      {inicio && inicio.proximos.length > 0 && (
        <section aria-labelledby="prox" className="bloco">
          <h2 id="prox">Próximos jogos</h2>
          <div className="card">
            <table>
              <tbody>
                {inicio.proximos.map(j => (
                  <tr key={j.id}>
                    <td className="l">{j.data ? `${diaDaSemana(j.data)} ${diaMes(j.data)}` : ''}{j.hora ? ` · ${j.hora}` : ''}</td>
                    <td className="l"><Link href={`/jogos/${j.id}`}><b>{j.casa.texto} × {j.visitante.texto}</b></Link></td>
                    <td className="l muted">{j.categoria} · {j.etiqueta}</td>
                    <td className="l muted">{j.local?.nome ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      <section aria-labelledby="ins" className="bloco cta">
        <div className="row between">
          <h2 id="ins" style={{ margin: 0 }}>Inscrições abertas</h2>
          {abertas && abertas.length > 0 && (
            <span className="badge ok">{abertas.length} {abertas.length === 1 ? 'temporada' : 'temporadas'}</span>
          )}
        </div>

        {abertas === null && (
          <p className="aviso" role="status" style={{ marginTop: 14 }}>
            Não foi possível carregar as inscrições agora. Tente de novo em instantes.
          </p>
        )}
        {abertas && abertas.length === 0 && (
          <p className="muted" style={{ marginTop: 14 }}>Não há inscrições abertas no momento.</p>
        )}
        {abertas && abertas.length > 0 && (
          <div className="grid g2" style={{ marginTop: 14 }}>
            {abertas.map(t => (
              <div className="card" style={{ margin: 0 }} key={t.temporadaId}>
                <h3>{t.campeonato} <span className="muted">· {t.ano}</span></h3>
                <p className="muted small" style={{ margin: '0 0 10px' }}>
                  {t.categorias.length} {t.categorias.length === 1 ? 'categoria' : 'categorias'} · inscrições de{' '}
                  <b>{dataBr(t.inicioInscricoes)}</b> a <b>{dataBr(t.fimInscricoes)}</b>
                </p>
                <p className="small" style={{ margin: '0 0 10px' }}>
                  {t.categorias.map(c => c.nome).join(' · ')}
                </p>
                <div className="row">
                  <span className="btn primary" role="link" aria-disabled="true">Inscrição online em breve</span>
                  <span className="small muted">
                    {t.diasRestantes === 0 ? 'Último dia' : `Prazo: ${t.diasRestantes} ${t.diasRestantes === 1 ? 'dia' : 'dias'}`}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      <div className="row" style={{ marginTop: 22 }}>
        <Link className="btn" href="/campeonatos">Ver campeonatos e classificação ›</Link>
      </div>
    </main>
  );
}

import { dataBr, obterAssociacao, obterInscricoesAbertas } from '@/lib/api';

export default async function Inicio() {
  const [associacao, abertas] = await Promise.all([obterAssociacao(), obterInscricoesAbertas()]);

  return (
    <main className="wrap">
      <h1>{associacao ? associacao.nome : 'Basquete Master'}</h1>
      <p className="sub">Inscrições, jogos e resultados.</p>

      <section aria-labelledby="ins" className="cta">
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

      <h2 style={{ marginTop: 28 }}>Em breve neste início</h2>
      <div className="grid g3">
        <div className="ph"><b>Jogos de hoje / ao vivo</b>Próxima entrega.</div>
        <div className="ph"><b>Últimos resultados</b>Próxima entrega.</div>
        <div className="ph"><b>Campeonatos e classificação</b>Próxima entrega.</div>
      </div>
    </main>
  );
}

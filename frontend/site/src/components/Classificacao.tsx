import type { ClassificacaoPublica, LinhaClassificacao } from '@/lib/api';

function Sequencia({ s }: { s: string }) {
  return (
    <span className="seq" aria-label={`Últimos resultados: ${s}`}>
      {s.split('').map((c, i) => <i key={i} className={c === 'V' ? 'V' : 'D'}>{c}</i>)}
    </span>
  );
}

function Tabela({ linhas }: { linhas: LinhaClassificacao[] }) {
  return (
    <div className="rolagem">
      <table>
        <thead>
          <tr><th>#</th><th className="l">Equipe</th><th>J</th><th>V</th><th>D</th><th>PF</th><th>PS</th><th>Saldo</th><th>Pts</th><th>Últ. 5</th></tr>
        </thead>
        <tbody>
          {linhas.map(l => (
            <tr key={`${l.posicao}-${l.texto}`} className={l.classificado ? 'cl' : undefined}>
              <td>{l.posicao}</td>
              <td className="l"><b>{l.texto}</b></td>
              <td>{l.jogos}</td><td>{l.vitorias}</td><td>{l.derrotas}</td>
              <td>{l.pontosFeitos}</td><td>{l.pontosSofridos}</td>
              <td>{l.saldo > 0 ? `+${l.saldo}` : l.saldo}</td>
              <td><b>{l.total}</b></td>
              <td><Sequencia s={l.sequencia} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Classificação da fase: tabela (pontos corridos), uma tabela por grupo ou as séries do mata-mata. Verde = classificada. */
export default function Classificacao({ dados }: { dados: ClassificacaoPublica }) {
  const c = dados.classificacao;
  if (!c) return <p className="muted">A tabela desta fase ainda não foi gerada.</p>;

  if (c.tipo === 'Tabela') {
    return (
      <div className="card">
        <Tabela linhas={c.linhas} />
        <p className="small muted" style={{ margin: '10px 0 0' }}>
          Verde: classificadas para a próxima fase. Pontos: vitória 2, derrota 1, W.O. 0. {c.definitiva ? '' : 'A fase ainda está em andamento.'}
        </p>
      </div>
    );
  }

  if (c.tipo === 'Grupos') {
    return (
      <>
        {c.grupos.map(g => (
          <div className="card" key={g.ordem} style={{ marginBottom: 14 }}>
            <h3>{g.nome}</h3>
            <Tabela linhas={g.linhas} />
          </div>
        ))}
        {c.melhores.length > 0 && (
          <div className="card">
            <h3>Melhores classificados dos grupos</h3>
            <Tabela linhas={c.melhores.map(m => ({ ...m.linha, posicao: m.posicao, texto: `${m.linha.texto} (${m.grupo})` }))} />
          </div>
        )}
        <p className="small muted">Verde: classificadas para a próxima fase.</p>
      </>
    );
  }

  return (
    <div className="grid g2">
      {c.confrontos.map(x => (
        <div className="card" key={x.id} style={{ margin: 0 }}>
          <h3>{x.nome} {x.decidido && <span className="badge ok">decidido</span>}</h3>
          <table>
            <tbody>
              <tr className={x.a.venceu ? 'cl' : undefined}><td className="l"><b>{x.a.texto}</b></td><td><b>{x.a.vitorias}</b></td></tr>
              <tr className={x.b.venceu ? 'cl' : undefined}><td className="l"><b>{x.b.texto}</b></td><td><b>{x.b.vitorias}</b></td></tr>
            </tbody>
          </table>
          <p className="small muted" style={{ margin: '8px 0 0' }}>Vence quem fizer {c.vitoriasNecessarias} {c.vitoriasNecessarias === 1 ? 'vitória' : 'vitórias'}.</p>
        </div>
      ))}
    </div>
  );
}

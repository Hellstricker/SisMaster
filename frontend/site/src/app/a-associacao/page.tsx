import type { Metadata } from 'next';
import { obterPagina } from '@/lib/api';

export const metadata: Metadata = { title: 'A associação' };

export default async function AAssociacao() {
  const p = await obterPagina();
  if (!p) {
    return (
      <main className="wrap">
        <h1>A associação</h1>
        <p className="aviso" role="status">Não foi possível carregar esta página agora. Tente de novo em instantes.</p>
      </main>
    );
  }
  const paragrafos = (p.quemSomos ?? '').split(/\n\s*\n/).map(x => x.trim()).filter(Boolean);
  const vazia = !p.missao && paragrafos.length === 0 && p.diretoria.length === 0;

  return (
    <main className="wrap">
      <h1>A associação</h1>
      <p className="sub">{p.associacao.nome} · {p.associacao.uf}</p>
      {vazia && <p className="muted">A associação ainda não publicou o conteúdo desta página.</p>}

      {p.missao && <div className="missao"><small>Nossa missão</small>{p.missao}</div>}

      {paragrafos.length > 0 && (
        <>
          <h2>Quem somos</h2>
          <div className="card">
            {paragrafos.map((t, i) => <p key={i} style={{ margin: i === 0 ? '0 0 12px' : '0 0 12px', whiteSpace: 'pre-line' }}>{t}</p>)}
          </div>
        </>
      )}

      {p.diretoria.length > 0 && (
        <>
          <h2>Diretoria{p.gestao ? <> <span className="muted small" style={{ fontWeight: 400 }}>· gestão {p.gestao}</span></> : null}</h2>
          <div className="grid g3">
            {p.diretoria.map(m => (
              <div className="card membro" key={`${m.cargo}-${m.nome}`} style={{ margin: 0 }}>
                <span className="cargo">{m.cargo}</span>
                <span className="nome">{m.nome}</span>
              </div>
            ))}
          </div>
        </>
      )}
    </main>
  );
}

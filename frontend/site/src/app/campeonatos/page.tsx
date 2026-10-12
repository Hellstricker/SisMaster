import type { Metadata } from 'next';
import Link from 'next/link';
import { obterCampeonatos } from '@/lib/api';

export const metadata: Metadata = { title: 'Campeonatos' };

const STATUS: Record<string, string> = {
  InscricoesAbertas: 'Inscrições abertas', InscricoesEncerradas: 'Inscrições encerradas', EmAndamento: 'Em andamento', Encerrada: 'Encerrado',
};

export default async function Campeonatos() {
  const temporadas = await obterCampeonatos();
  return (
    <main className="wrap">
      <h1>Campeonatos</h1>
      <p className="sub">Jogos, classificação e equipes de cada temporada.</p>
      {temporadas === null && <p className="aviso" role="status">Não foi possível carregar os campeonatos agora. Tente de novo em instantes.</p>}
      {temporadas && temporadas.length === 0 && <p className="muted">Ainda não há campeonatos publicados.</p>}
      {temporadas && temporadas.length > 0 && (
        <div className="grid g2">
          {temporadas.map(t => (
            <Link key={t.id} href={`/campeonatos/${t.id}`} className="card" style={{ margin: 0, textDecoration: 'none', display: 'block' }}>
              <h3>{t.campeonato} <span className="muted">· {t.ano}</span></h3>
              <span className={`badge ${t.status === 'EmAndamento' ? 'ok' : ''}`}>{STATUS[t.status] ?? t.status}</span>
            </Link>
          ))}
        </div>
      )}
    </main>
  );
}

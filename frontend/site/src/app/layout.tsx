import type { Metadata } from 'next';
import Link from 'next/link';
import './globals.css';
import { obterAssociacao } from '@/lib/api';

export const dynamic = 'force-dynamic'; // os dados vêm da API em tempo de acesso, não do build

export async function generateMetadata(): Promise<Metadata> {
  const a = await obterAssociacao();
  return {
    title: { default: a ? `${a.sigla} — ${a.nome}` : 'SisMaster', template: `%s · ${a?.sigla ?? 'SisMaster'}` },
    description: a ? `Site oficial da ${a.nome}: inscrições, jogos e resultados.` : 'Basquete Master: inscrições, jogos e resultados.',
  };
}

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const a = await obterAssociacao();
  const sigla = a?.sigla ?? 'SisMaster';
  return (
    <html lang="pt-BR">
      <body>
        <header className="top">
          <div className="wrap">
            <Link className="logo" href="/"><i />{sigla}</Link>
            <nav aria-label="Principal">
              <Link href="/" aria-current="page">Início</Link>
            </nav>
          </div>
        </header>
        {children}
        <footer className="rodape">
          <div className="wrap">
            <span>{a ? `${a.nome} · ${a.uf}` : 'SisMaster'}</span>
            <span><a href="/admin/">Gestão</a> · <a href="/arbitros/">Árbitros</a></span>
          </div>
        </footer>
      </body>
    </html>
  );
}

import type { Metadata } from 'next';
import Link from 'next/link';
import './globals.css';
import ContaDoUsuario from '@/components/conta/ContaDoUsuario';
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
              <Link href="/">Início</Link>
              <Link href="/campeonatos">Campeonatos</Link>
              <Link href="/a-associacao">A associação</Link>
              <Link href="/contato">Contato</Link>
            </nav>
            <ContaDoUsuario />
          </div>
        </header>
        {children}
        <footer className="rodape">
          <div className="wrap">
            <span>{a ? `${a.nome} · ${a.uf}` : 'SisMaster'}</span>
            <span>
              <Link href="/privacidade">Privacidade</Link> · <Link href="/termos">Termos de uso</Link> · <a href="/admin/">Gestão</a> · <a href="/arbitros/">Árbitros</a>
            </span>
          </div>
        </footer>
      </body>
    </html>
  );
}

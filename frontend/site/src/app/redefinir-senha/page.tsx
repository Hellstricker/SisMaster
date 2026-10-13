import type { Metadata } from 'next';
import RedefinirSenha from '@/components/conta/RedefinirSenha';

export const metadata: Metadata = { title: 'Nova senha' };

export default async function Pagina({ searchParams }: { searchParams: Promise<{ email?: string; token?: string }> }) {
  const { email, token } = await searchParams;
  return <main className="wrap"><RedefinirSenha email={email ?? null} token={token ?? null} /></main>;
}

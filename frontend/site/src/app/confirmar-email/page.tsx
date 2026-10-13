import type { Metadata } from 'next';
import ConfirmarEmail from '@/components/conta/ConfirmarEmail';

export const metadata: Metadata = { title: 'Confirmar e-mail' };

export default async function Pagina({ searchParams }: { searchParams: Promise<{ email?: string; token?: string; voltar?: string }> }) {
  const { email, token, voltar } = await searchParams;
  return (
    <main className="wrap">
      <ConfirmarEmail email={email ?? null} token={token ?? null} voltar={voltar ?? null} />
    </main>
  );
}

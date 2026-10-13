import type { Metadata } from 'next';
import EntrarOuCriar from '@/components/conta/EntrarOuCriar';
import { obterTemporada } from '@/lib/api';

export const metadata: Metadata = { title: 'Entrar ou criar conta' };

export default async function Entrar({ searchParams }: { searchParams: Promise<{ aba?: string; voltar?: string }> }) {
  const { aba, voltar } = await searchParams;

  // Vindo da inscrição, mostra qual campeonato a pessoa escolheu.
  let contexto: string | null = null;
  const m = voltar?.match(/^\/inscricao\/([0-9a-fA-F-]{36})$/);
  if (m) {
    const t = await obterTemporada(m[1]);
    if (t) contexto = `Você escolheu ${t.campeonato} · ${t.ano}. Crie sua conta (leva 1 minuto) e depois você volta direto para a inscrição.`;
  }

  return (
    <main className="wrap">
      <EntrarOuCriar abaInicial={aba === 'entrar' ? 'entrar' : 'criar'} voltar={voltar ?? null} contexto={contexto} />
    </main>
  );
}

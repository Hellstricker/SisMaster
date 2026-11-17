import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import Assistente from '@/components/inscricao/Assistente';
import ComoParticipar from '@/components/inscricao/ComoParticipar';
import { obterInscricoesAbertas, obterLegal, obterTemporada } from '@/lib/api';

export const metadata: Metadata = { title: 'Inscrição' };

export default async function Pagina({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ modo?: string }> }) {
  const { id } = await params;
  const { modo } = await searchParams;
  const t = await obterTemporada(id);
  if (!t) notFound();

  // Torneio aberto com inscrições abertas: antes do formulário, a pessoa escolhe se participa por uma equipe ou como atleta avulso.
  if (t.publico === 'Aberto' && modo !== 'individual') {
    const aberta = (await obterInscricoesAbertas())?.find(x => x.temporadaId === id);
    if (aberta) return <main className="wrap"><ComoParticipar t={aberta} /></main>;
  }

  const legal = await obterLegal();
  return <main className="wrap"><Assistente temporadaId={id} versaoTermos={legal.versao} /></main>;
}

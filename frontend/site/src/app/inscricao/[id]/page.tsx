import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import InscricaoEmBreve from '@/components/conta/InscricaoEmBreve';
import { obterTemporada } from '@/lib/api';

export const metadata: Metadata = { title: 'Inscrição' };

export default async function Pagina({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await obterTemporada(id);
  if (!t) notFound();
  return <main className="wrap"><InscricaoEmBreve temporadaId={id} campeonato={`${t.campeonato} · ${t.ano}`} /></main>;
}

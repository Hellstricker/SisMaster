import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import Assistente from '@/components/inscricao/Assistente';
import { obterLegal, obterTemporada } from '@/lib/api';

export const metadata: Metadata = { title: 'Inscrição' };

export default async function Pagina({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await obterTemporada(id);
  if (!t) notFound();
  const legal = await obterLegal();
  return <main className="wrap"><Assistente temporadaId={id} versaoTermos={legal.versao} /></main>;
}

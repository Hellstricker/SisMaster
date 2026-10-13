import type { Metadata } from 'next';
import DetalheDaInscricao from '@/components/inscricao/DetalheDaInscricao';

export const metadata: Metadata = { title: 'Detalhe da inscrição' };

export default async function Pagina({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <main className="wrap"><DetalheDaInscricao id={id} /></main>;
}

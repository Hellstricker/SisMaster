import type { Metadata } from 'next';
import MinhaEquipe from '@/components/inscricao/MinhaEquipe';

export const metadata: Metadata = { title: 'Minha equipe' };

export default async function Pagina({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <main className="wrap"><MinhaEquipe id={id} /></main>;
}

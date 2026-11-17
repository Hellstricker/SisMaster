import type { Metadata } from 'next';
import ConviteDoAtleta from '@/components/inscricao/ConviteDoAtleta';
import { obterLegal } from '@/lib/api';

export const metadata: Metadata = { title: 'Convite para a equipe', robots: { index: false } };

export default async function Pagina({ params }: { params: Promise<{ codigo: string }> }) {
  const { codigo } = await params;
  const legal = await obterLegal();
  return <main className="wrap"><ConviteDoAtleta codigo={codigo} versaoTermos={legal.versao} /></main>;
}

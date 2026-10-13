import type { Metadata } from 'next';
import MinhasInscricoes from '@/components/inscricao/MinhasInscricoes';

export const metadata: Metadata = { title: 'Minhas inscrições' };

export default function Pagina() {
  return <main className="wrap"><MinhasInscricoes /></main>;
}

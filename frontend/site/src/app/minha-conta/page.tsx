import type { Metadata } from 'next';
import MinhaConta from '@/components/conta/MinhaConta';

export const metadata: Metadata = { title: 'Minha conta' };

export default function Pagina() {
  return <main className="wrap"><MinhaConta /></main>;
}

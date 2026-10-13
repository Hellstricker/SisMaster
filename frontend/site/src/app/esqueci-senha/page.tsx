import type { Metadata } from 'next';
import EsqueciSenha from '@/components/conta/EsqueciSenha';

export const metadata: Metadata = { title: 'Esqueci a senha' };

export default function Pagina() {
  return <main className="wrap"><EsqueciSenha /></main>;
}

import Link from 'next/link';

export default function NaoEncontrada() {
  return (
    <main className="wrap">
      <h1>Página não encontrada</h1>
      <p className="sub">O endereço não existe ou foi mudado.</p>
      <Link className="btn primary" href="/">Voltar ao início</Link>
    </main>
  );
}

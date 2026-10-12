'use client';

import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

/** Recarrega os dados da página a cada alguns segundos (só com a aba visível), para o placar e a lista de jogos acompanharem o jogo. */
export default function AtualizarSozinho({ segundos }: { segundos: number }) {
  const router = useRouter();
  useEffect(() => {
    const t = setInterval(() => {
      if (document.visibilityState === 'visible') router.refresh();
    }, segundos * 1000);
    return () => clearInterval(t);
  }, [router, segundos]);
  return null;
}

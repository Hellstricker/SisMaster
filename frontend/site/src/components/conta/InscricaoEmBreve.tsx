'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { lerSessao, type SessaoDoSite } from '@/lib/sessao';

/**
 * Porta de entrada da inscrição: exige conta. Sem sessão leva a "Entrar / criar conta" e volta para cá. O assistente de inscrição
 * (dados da ficha, categorias, autorizações e revisão) é a próxima entrega; por enquanto mostra que a conta está pronta.
 */
export default function InscricaoEmBreve({ temporadaId, campeonato }: { temporadaId: string; campeonato: string }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  useEffect(() => { setSessao(lerSessao()); }, []);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Inscrição</h1>
        <p>{campeonato}</p>
        <p className="muted">Para se inscrever você precisa de uma conta.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent(`/inscricao/${temporadaId}`)}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  return (
    <div className="card conta centro">
      <div className="icone" aria-hidden="true">📝</div>
      <h1>Inscrição — {campeonato}</h1>
      <p>Olá, <b>{sessao.usuario.nome.split(' ')[0]}</b>! Sua conta está pronta ({sessao.usuario.email}).</p>
      <p className="muted">O formulário de inscrição (dados da ficha, categorias e autorizações) será liberado na próxima etapa.</p>
    </div>
  );
}

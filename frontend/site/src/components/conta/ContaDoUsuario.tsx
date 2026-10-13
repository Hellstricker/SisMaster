'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { aoMudarSessao, lerSessao, sair, type SessaoDoSite } from '@/lib/sessao';

/** No cabeçalho: "Entrar" para visitante; para quem está conectado, o nome e "Sair". */
export default function ContaDoUsuario() {
  const [sessao, setSessao] = useState<SessaoDoSite | null>(null);
  const [pronto, setPronto] = useState(false);

  useEffect(() => {
    const atualizar = () => setSessao(lerSessao());
    atualizar();
    setPronto(true);
    return aoMudarSessao(atualizar);
  }, []);

  if (!pronto) return <span className="conta-topo" />;
  if (!sessao) return <span className="conta-topo"><Link className="btn ghost" href="/entrar?aba=entrar">Entrar</Link></span>;
  return (
    <span className="conta-topo">
      <span className="nome" title={sessao.usuario.email}>{sessao.usuario.nome.split(' ')[0]}</span>
      <button className="btn ghost" type="button" onClick={() => sair()}>Sair</button>
    </span>
  );
}

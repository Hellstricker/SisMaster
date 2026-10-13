'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useEffect, useState } from 'react';
import { aceitarTermos, aoMudarSessao, atualizarSessao, lerSessao, sair, type SessaoDoSite } from '@/lib/sessao';

/** Número principal da versão: "2.1" -> 2. Versão desconhecida ("?" ou vazia) conta como 0 e pede aceite. */
export function principalDaVersao(v: string | null | undefined): number {
  const n = parseInt((v ?? '').split('.')[0], 10);
  return Number.isFinite(n) ? n : 0;
}

/**
 * Pede novo aceite dos Termos e da Política quando sai uma versão com número principal maior que a que a conta aceitou (mudança
 * relevante; mudança pequena, como 1.0 para 1.1, não pede nada). Nas páginas dos próprios textos aparece só uma faixa, para dar
 * para ler; nas demais, uma janela que segura o uso até a pessoa aceitar ou sair.
 */
export default function GuardaDeAceite({ versao, resumo }: { versao: string; resumo: string | null }) {
  const caminho = usePathname();
  const [sessao, setSessao] = useState<SessaoDoSite | null>(null);
  const [erro, setErro] = useState('');
  const [ocupado, setOcupado] = useState(false);

  useEffect(() => {
    const atualizar = () => setSessao(lerSessao());
    atualizar();
    return aoMudarSessao(atualizar);
  }, []);

  // Sessão aberta antes desta função existir não sabe a versão aceita: um token novo traz o dado.
  const desconhecida = sessao !== null && sessao.usuario.termosVersao === undefined;
  useEffect(() => { if (desconhecida) void atualizarSessao().then(s => setSessao(s)); }, [desconhecida]);

  if (!sessao || desconhecida) return null;
  if (principalDaVersao(sessao.usuario.termosVersao) >= principalDaVersao(versao)) return null;

  async function aceitar() {
    setErro('');
    setOcupado(true);
    const r = await aceitarTermos(versao);
    setOcupado(false);
    if (!r.ok) setErro(r.erro);
  }

  const lendo = caminho === '/privacidade' || caminho === '/termos';
  const botoes = (
    <>
      <button className="btn primary" type="button" disabled={ocupado} onClick={aceitar}>Li e aceito a versão {versao}</button>
      <button className="btn" type="button" onClick={() => sair()}>Sair da conta</button>
    </>
  );

  if (lendo) {
    return (
      <div className="faixa-aceite" role="status">
        <span>Atualizamos os Termos de uso e a Política de privacidade (versão {versao}). Leia e aceite para continuar usando a sua conta.</span>
        {botoes}
        {erro && <span className="aviso bad" role="alert">{erro}</span>}
      </div>
    );
  }

  return (
    <div className="aceite-fundo">
      <div className="card aceite-janela" role="dialog" aria-modal="true" aria-labelledby="aceite-titulo">
        <h2 id="aceite-titulo">Atualizamos os nossos termos</h2>
        <p>
          Os <Link href="/termos">Termos de uso</Link> e a <Link href="/privacidade">Política de privacidade</Link> mudaram (versão {versao}).
          Para continuar usando a sua conta, leia o que mudou e aceite.
        </p>
        {resumo && <p className="aviso info"><b>O que mudou:</b> {resumo}</p>}
        {erro && <p className="aviso bad" role="alert">{erro}</p>}
        <div className="aceite-botoes">{botoes}</div>
      </div>
    </div>
  );
}

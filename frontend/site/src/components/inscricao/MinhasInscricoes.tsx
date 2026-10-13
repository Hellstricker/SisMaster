'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { type MinhasInscricoes as Dados, ROTULO_FICHA, ROTULO_PEDIDO, dataCurta } from '@/lib/inscricao';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

/** A lista das inscrições da conta, categoria por categoria (I6). */
export default function MinhasInscricoes() {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [dados, setDados] = useState<Dados | null>(null);
  const [erro, setErro] = useState('');

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (!s) return;
    void chamarApi<Dados>('GET', '/api/participante/inscricoes').then(r => {
      if (r.ok) setDados(r.dados);
      else { setErro(r.erro); if (r.expirou) setSessao(null); }
    });
  }, []);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Minhas inscrições</h1>
        <p className="muted">Entre na sua conta para ver as suas inscrições.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent('/minhas-inscricoes')}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  if (erro) return <div className="card conta"><p className="aviso bad" role="alert">{erro}</p></div>;
  if (!dados) return <div className="card conta centro"><p>Carregando…</p></div>;

  return (
    <>
      <div className="linha entre">
        <div><h1>Minhas inscrições</h1><p className="muted">Acompanhe cada pedido, categoria por categoria.</p></div>
        <Link className="btn primary" href="/#ins">Nova inscrição</Link>
      </div>

      {dados.acaoNecessaria && (
        <p className="aviso warn" role="status"><b>Ação necessária:</b> uma inscrição foi aprovada e aguarda o pagamento da taxa. A associação envia as instruções de pagamento.</p>
      )}

      {dados.inscricoes.length === 0 ? (
        <div className="card vazio">
          <p><b>Você ainda não fez nenhuma inscrição.</b></p>
          <p className="muted">Veja as temporadas com inscrições abertas.</p>
          <Link className="btn primary" href="/#ins">Ver inscrições abertas</Link>
        </div>
      ) : dados.inscricoes.map(i => (
        <div className="card ficha" key={i.id} data-protocolo={i.protocolo ?? ''}>
          <div className="linha entre">
            <div>
              <h3>{i.campeonato} · {i.ano}</h3>
              <div className="muted small">
                {i.protocolo ? `protocolo #${i.protocolo} · ` : ''}enviada em {dataCurta(i.enviadaEm)}{i.origem === 'Diretoria' ? ' · feita pela diretoria' : ''}
              </div>
            </div>
            <span className={`badge st-${i.situacao}`}>{ROTULO_FICHA[i.situacao]}</span>
          </div>
          <div className="cats">
            {i.categorias.map(c => <span key={c.id}>{c.nome} <b className={`badge st-${c.status}`}>{ROTULO_PEDIDO[c.status]}</b></span>)}
          </div>
        </div>
      ))}
    </>
  );
}

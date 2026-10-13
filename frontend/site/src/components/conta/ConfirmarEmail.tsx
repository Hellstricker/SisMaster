'use client';

import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { caminhoSeguro, enviarParaIdentidade, iniciarSessao } from '@/lib/sessao';

/** Abre o link do e-mail: confirma, já conecta a pessoa e a leva de volta à inscrição. Link vencido ou usado: pede um novo. */
export default function ConfirmarEmail({ email, token, voltar }: { email: string | null; token: string | null; voltar: string | null }) {
  const destino = caminhoSeguro(voltar);
  const [estado, setEstado] = useState<'confirmando' | 'ok' | 'invalido'>('confirmando');
  const [erro, setErro] = useState('');
  const [novoEmail, setNovoEmail] = useState(email ?? '');
  const [aviso, setAviso] = useState('');
  const feito = useRef(false);

  useEffect(() => {
    if (feito.current) return;   // o link é de uso único: não confirmar duas vezes (em desenvolvimento o React roda o efeito 2x)
    feito.current = true;
    if (!email || !token) { setEstado('invalido'); return; }
    enviarParaIdentidade<Parameters<typeof iniciarSessao>[0]>('confirmar-email', { email, token }).then(r => {
      if (!r.ok) { setErro(r.erro); setEstado('invalido'); return; }
      const s = iniciarSessao(r.dados);
      if (!s.ok) { setErro(s.erro); setEstado('invalido'); return; }
      setEstado('ok');
    });
  }, [email, token]);

  async function pedirNovo() {
    setAviso(''); setErro('');
    const r = await enviarParaIdentidade('reenviar-confirmacao', { email: novoEmail.trim(), voltar: destino });
    if (r.ok) setAviso('Se o e-mail tiver uma conta ainda não confirmada, enviamos um novo link.'); else setErro(r.erro);
  }

  if (estado === 'confirmando') return <div className="card conta centro"><p>Confirmando seu e-mail…</p></div>;

  if (estado === 'ok') {
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✅</div>
        <h1>E-mail confirmado</h1>
        <p>Sua conta está ativa e você já está conectado.{destino ? ' Vamos continuar a inscrição.' : ''}</p>
        <Link className="btn primary block" href={destino ?? '/'}>{destino ? 'Continuar a inscrição' : 'Ir para o início'}</Link>
      </div>
    );
  }

  return (
    <div className="card conta centro">
      <div className="icone" aria-hidden="true">⏳</div>
      <h1>Link inválido ou vencido</h1>
      <p>O link de confirmação vale por 24 horas e só pode ser usado uma vez. Peça um novo:</p>
      {erro && <p className="aviso bad" role="alert">{erro}</p>}
      {aviso && <p className="aviso ok" role="status">{aviso}</p>}
      <div className="f" style={{ textAlign: 'left' }}>
        <label htmlFor="n-email">E-mail da conta</label>
        <input id="n-email" type="email" value={novoEmail} onChange={e => setNovoEmail(e.target.value)} />
      </div>
      <button className="btn primary block" type="button" onClick={pedirNovo}>Enviar novo link</button>
      <p className="small" style={{ margin: '12px 0 0' }}><Link href="/entrar?aba=entrar">Já confirmei: entrar</Link></p>
    </div>
  );
}

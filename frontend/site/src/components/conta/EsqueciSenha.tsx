'use client';

import Link from 'next/link';
import { useState, type FormEvent } from 'react';
import { enviarParaIdentidade } from '@/lib/sessao';

export default function EsqueciSenha() {
  const [email, setEmail] = useState('');
  const [enviado, setEnviado] = useState(false);
  const [erro, setErro] = useState('');
  const [ocupado, setOcupado] = useState(false);

  async function enviar(e: FormEvent) {
    e.preventDefault();
    setErro('');
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) return setErro('Informe um e-mail válido.');
    setOcupado(true);
    const r = await enviarParaIdentidade('esqueci-senha', { email: email.trim() });
    setOcupado(false);
    if (r.ok) setEnviado(true); else setErro(r.erro);
  }

  return (
    <div className="card conta">
      <h1>Esqueci a senha</h1>
      <p className="muted">Informe o e-mail da conta. Se ele estiver cadastrado, enviamos um link (vale 1 hora) para definir uma nova senha. Sua senha atual continua valendo até você usar o link.</p>
      {erro && <p className="aviso bad" role="alert">{erro}</p>}
      {enviado ? (
        <p className="aviso ok" role="status">Se o e-mail estiver cadastrado, enviamos um link para definir uma nova senha.</p>
      ) : (
        <form onSubmit={enviar} noValidate>
          <div className="f"><label htmlFor="e-email">E-mail</label><input id="e-email" type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} /></div>
          <button className="btn primary block" type="submit" disabled={ocupado}>{ocupado ? 'Enviando...' : 'Enviar link'}</button>
        </form>
      )}
      <p className="small centro" style={{ margin: '12px 0 0' }}><Link href="/entrar?aba=entrar">Voltar para entrar</Link></p>
    </div>
  );
}

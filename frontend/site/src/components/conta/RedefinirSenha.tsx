'use client';

import Link from 'next/link';
import { useState, type FormEvent } from 'react';
import { enviarParaIdentidade } from '@/lib/sessao';
import CampoSenha, { senhaValida } from './CampoSenha';

export default function RedefinirSenha({ email, token }: { email: string | null; token: string | null }) {
  const [senha, setSenha] = useState('');
  const [repete, setRepete] = useState('');
  const [erro, setErro] = useState('');
  const [pronto, setPronto] = useState(false);
  const [ocupado, setOcupado] = useState(false);

  if (!email || !token) {
    return (
      <div className="card conta centro">
        <h1>Link inválido</h1>
        <p>Este link está incompleto. Peça um novo em “Esqueci a senha”.</p>
        <Link className="btn primary block" href="/esqueci-senha">Pedir novo link</Link>
      </div>
    );
  }

  async function enviar(e: FormEvent) {
    e.preventDefault();
    setErro('');
    if (!senhaValida(senha)) return setErro('A senha precisa de 8 caracteres, com letra e número.');
    if (senha !== repete) return setErro('As duas senhas não são iguais.');
    setOcupado(true);
    const r = await enviarParaIdentidade('redefinir-senha', { email, token, novaSenha: senha });
    setOcupado(false);
    if (r.ok) setPronto(true); else setErro(r.erro);
  }

  if (pronto) {
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✅</div>
        <h1>Senha alterada</h1>
        <p>Entre com a nova senha.</p>
        <Link className="btn primary block" href="/entrar?aba=entrar">Entrar</Link>
      </div>
    );
  }

  return (
    <div className="card conta">
      <h1>Nova senha</h1>
      <p className="muted">Conta: <b>{email}</b></p>
      {erro && <p className="aviso bad" role="alert">{erro} {erro.includes('link') && <Link href="/esqueci-senha">Pedir novo link</Link>}</p>}
      <form onSubmit={enviar} noValidate>
        <CampoSenha id="r-senha" rotulo="Nova senha" valor={senha} aoMudar={setSenha} nova />
        <CampoSenha id="r-repete" rotulo="Repita a nova senha" valor={repete} aoMudar={setRepete} autoComplete="new-password" />
        <button className="btn primary block" type="submit" disabled={ocupado}>{ocupado ? 'Salvando...' : 'Salvar nova senha'}</button>
      </form>
    </div>
  );
}

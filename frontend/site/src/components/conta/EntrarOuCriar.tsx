'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { caminhoSeguro, entrar, enviarParaIdentidade } from '@/lib/sessao';
import CampoSenha, { senhaValida } from './CampoSenha';


type Aba = 'criar' | 'entrar';

/**
 * Entrar ou criar conta. Vindo da inscrição, o cadastro acontece aqui mesmo: cria a conta, pede para confirmar o e-mail e,
 * ao confirmar, a pessoa volta direto para o passo seguinte da inscrição (`voltar`).
 */
export default function EntrarOuCriar({ abaInicial, voltar, contexto, versaoTermos }: { abaInicial: Aba; voltar: string | null; contexto: string | null; versaoTermos: string }) {
  const router = useRouter();
  const destino = caminhoSeguro(voltar);
  const [aba, setAba] = useState<Aba>(abaInicial);
  const [enviado, setEnviado] = useState<string | null>(null);   // e-mail para o qual o link de confirmação foi enviado

  // criar
  const [nome, setNome] = useState('');
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [aceite, setAceite] = useState(false);
  const [isca, setIsca] = useState('');
  // entrar
  const [emailLogin, setEmailLogin] = useState('');
  const [senhaLogin, setSenhaLogin] = useState('');

  const [erro, setErro] = useState('');
  const [naoConfirmado, setNaoConfirmado] = useState(false);
  const [aviso, setAviso] = useState('');
  const [ocupado, setOcupado] = useState(false);

  async function criar(e: FormEvent) {
    e.preventDefault();
    setErro('');
    if (nome.trim().length < 3) return setErro('Informe o nome completo.');
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) return setErro('E-mail inválido.');
    if (!senhaValida(senha)) return setErro('A senha precisa de 8 caracteres, com letra e número.');
    if (!aceite) return setErro('É preciso aceitar os Termos de uso e a Política de privacidade.');
    setOcupado(true);
    const r = await enviarParaIdentidade('cadastrar', { nome: nome.trim(), email: email.trim(), senha, aceiteTermos: aceite, versaoTermos, voltar: destino, site: isca });
    setOcupado(false);
    if (!r.ok) return setErro(r.erro);
    setEnviado(email.trim());
  }

  async function logar(e: FormEvent) {
    e.preventDefault();
    setErro(''); setNaoConfirmado(false); setAviso('');
    if (!/^\S+@\S+\.\S+$/.test(emailLogin.trim())) return setErro('Informe um e-mail válido.');
    if (!senhaLogin) return setErro('Informe a senha.');
    setOcupado(true);
    const r = await entrar(emailLogin.trim(), senhaLogin);
    if (r.ok) return router.push(destino ?? '/');
    setOcupado(false);
    setErro(r.erro);
    setNaoConfirmado(r.naoConfirmado);
  }

  async function reenviar(endereco: string) {
    setErro(''); setAviso('');
    const r = await enviarParaIdentidade('reenviar-confirmacao', { email: endereco, voltar: destino });
    if (r.ok) setAviso('Se o e-mail tiver uma conta ainda não confirmada, enviamos um novo link. Você poderá pedir outro em 10 minutos.');
    else setErro(r.erro);
  }

  if (enviado) {
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✉️</div>
        <h1>Confirme seu e-mail</h1>
        <p>Se o e-mail <b>{enviado}</b> ainda não tiver conta, enviamos um link para confirmá-lo. Abra o e-mail e clique no link: ele vale por 24 horas.</p>
        <p className="muted small">Não chegou? Veja a caixa de spam. Se o e-mail já tiver conta, use <button className="link" onClick={() => { setEnviado(null); setAba('entrar'); setEmailLogin(enviado); }}>Entrar</button> ou <Link href="/esqueci-senha">Esqueci a senha</Link>.</p>
        {erro && <p className="aviso bad" role="alert">{erro}</p>}
        {aviso && <p className="aviso ok" role="status">{aviso}</p>}
        <button className="btn block" type="button" onClick={() => reenviar(enviado)}>Reenviar e-mail</button>
        <p style={{ margin: '12px 0 0' }}><button className="link small" onClick={() => setEnviado(null)}>Digitei o e-mail errado</button></p>
      </div>
    );
  }

  return (
    <>
      {contexto && <div className="contexto">{contexto}</div>}
      <div className="card conta">
        <div className="abas-conta" role="tablist">
          <button role="tab" aria-selected={aba === 'criar'} className={aba === 'criar' ? 'on' : ''} onClick={() => { setAba('criar'); setErro(''); }}>Criar conta</button>
          <button role="tab" aria-selected={aba === 'entrar'} className={aba === 'entrar' ? 'on' : ''} onClick={() => { setAba('entrar'); setErro(''); }}>Já tenho conta</button>
        </div>
        {erro && <p className="aviso bad" role="alert">{erro}{naoConfirmado && <> <button className="link" onClick={() => reenviar(emailLogin.trim())}>Reenviar e-mail de confirmação</button></>}</p>}
        {aviso && <p className="aviso ok" role="status">{aviso}</p>}

        {aba === 'criar' ? (
          <form onSubmit={criar} noValidate>
            <div className="f"><label htmlFor="c-nome">Nome completo</label><input id="c-nome" type="text" autoComplete="name" value={nome} onChange={e => setNome(e.target.value)} /></div>
            <div className="f"><label htmlFor="c-email">E-mail</label><input id="c-email" type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} /><span className="hint">Enviamos um link de confirmação para este e-mail.</span></div>
            <CampoSenha id="c-senha" valor={senha} aoMudar={setSenha} nova />
            {/* Campo-isca: invisível para pessoas; robôs costumam preenchê-lo e o cadastro é ignorado */}
            <input className="isca" type="text" name="site" tabIndex={-1} autoComplete="off" aria-hidden="true" value={isca} onChange={e => setIsca(e.target.value)} />
            <label className="check">
              <input type="checkbox" checked={aceite} onChange={e => setAceite(e.target.checked)} />
              <span>Li e aceito os <Link href="/termos" target="_blank">Termos de uso</Link> e a <Link href="/privacidade" target="_blank">Política de privacidade</Link> (versão {versaoTermos}).</span>
            </label>
            <button className="btn primary block" type="submit" disabled={!aceite || ocupado}>{ocupado ? 'Criando...' : 'Criar conta e continuar'}</button>
            <p className="small muted centro" style={{ margin: '14px 0 0' }}>Só pedimos nome, e-mail e senha agora. CPF e demais dados vêm na inscrição.</p>
          </form>
        ) : (
          <form onSubmit={logar} noValidate>
            <div className="f"><label htmlFor="l-email">E-mail</label><input id="l-email" type="email" autoComplete="email" value={emailLogin} onChange={e => setEmailLogin(e.target.value)} /></div>
            <CampoSenha id="l-senha" valor={senhaLogin} aoMudar={setSenhaLogin} />
            <button className="btn primary block" type="submit" disabled={ocupado}>{ocupado ? 'Entrando...' : 'Entrar'}</button>
            <p className="small centro" style={{ margin: '12px 0 0' }}><Link href="/esqueci-senha">Esqueci a senha</Link></p>
          </form>
        )}
      </div>
    </>
  );
}

import { useState, type FormEvent } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { destinoAposEntrar, useAuth } from '../../auth/AuthContext';
import { esqueciSenha } from '../../auth/sessao';
import { AREA_ARBITROS } from '../../utils/area';
import styles from './Acesso.module.css';

/** Tela 14 — login da gestão e da área do árbitro (mesma tela; o perfil decide para onde a pessoa vai). */
export default function Login() {
  const { usuario, entrar } = useAuth();
  const [busca] = useSearchParams();
  const navigate = useNavigate();
  const voltar = busca.get('voltar');

  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [ver, setVer] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState('');
  const [esqueci, setEsqueci] = useState(false);
  const [pedido, setPedido] = useState<'' | 'enviando' | 'enviado'>('');

  // já logado: segue para a área certa
  if (usuario) {
    const d = destinoAposEntrar(usuario, voltar);
    if (d.externo) { window.location.replace(d.caminho); return null; }
    return <Navigate to={d.caminho} replace />;
  }

  async function enviar(e: FormEvent) {
    e.preventDefault();
    setErro('');
    if (!/\S+@\S+\.\S+/.test(email)) { setErro('Informe um e-mail válido.'); return; }
    if (!senha) { setErro('Informe a senha.'); return; }
    setEnviando(true);
    try {
      const sessao = await entrar(email.trim(), senha);
      const d = destinoAposEntrar(sessao.usuario, voltar);
      if (d.externo) window.location.replace(d.caminho);
      else navigate(d.caminho, { replace: true });
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível entrar.');
      setEnviando(false);
    }
  }

  async function recuperar() {
    setErro('');
    if (!/\S+@\S+\.\S+/.test(email)) { setErro('Informe o seu e-mail acima para receber a senha temporária.'); return; }
    setPedido('enviando');
    try {
      await esqueciSenha(email.trim());
      setPedido('enviado');
    } catch (ex) {
      setPedido('');
      setErro(ex instanceof Error ? ex.message : 'Não foi possível enviar o pedido.');
    }
  }

  return (
    <div className={styles.centro}>
      <div className={styles.caixa}>
        <div className={styles.marca}>
          <b>SisMaster</b>
          <p>{AREA_ARBITROS ? 'Área do árbitro' : 'Gestão de campeonatos de basquete'}</p>
        </div>

        <div className={styles.card}>
          {voltar && !erro && <div className={`${styles.aviso} ${styles.info}`}>Entre para continuar.</div>}
          {erro && <div className={`${styles.aviso} ${styles.erro}`} role="alert">{erro}</div>}

          <form onSubmit={enviar} noValidate>
            <div className={styles.campo}>
              <label htmlFor="email">E-mail</label>
              <input id="email" type="email" autoComplete="username" value={email} onChange={e => setEmail(e.target.value)} autoFocus />
            </div>
            <div className={styles.campo}>
              <label htmlFor="senha">Senha</label>
              <div className={styles.senha}>
                <input id="senha" type={ver ? 'text' : 'password'} autoComplete="current-password" value={senha} onChange={e => setSenha(e.target.value)} />
                <button type="button" onClick={() => setVer(v => !v)}>{ver ? 'Ocultar' : 'Mostrar'}</button>
              </div>
            </div>
            <button className={styles.botao} disabled={enviando}>{enviando ? 'Entrando…' : 'Entrar'}</button>
          </form>

          <p className={styles.rodape}><a onClick={() => { setEsqueci(true); setPedido(''); }}>Esqueci minha senha</a></p>
          {esqueci && (
            <div className={`${styles.aviso} ${styles.info}`} style={{ marginTop: 12, marginBottom: 0 }}>
              {pedido === 'enviado'
                ? 'Se o e-mail estiver cadastrado, enviamos uma senha temporária para ele. Entre com ela e defina a sua senha. Você pode pedir de novo depois de 10 minutos.'
                : (
                  <>
                    Informe o seu e-mail no campo acima e peça uma senha temporária. Ela chega por e-mail e deve ser trocada ao entrar.
                    <div style={{ marginTop: 8 }}>
                      <button type="button" className={styles.botao} onClick={recuperar} disabled={pedido === 'enviando'}>
                        {pedido === 'enviando' ? 'Enviando…' : 'Enviar senha temporária'}
                      </button>
                    </div>
                  </>
                )}
            </div>
          )}
        </div>
        {AREA_ARBITROS && <p className={styles.rodape}>Administrador? <a href="/admin/entrar">Entre pela gestão</a>.</p>}
      </div>
    </div>
  );
}

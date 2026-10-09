import { useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { redefinirSenha } from '../../auth/sessao';
import { AREA_ARBITROS } from '../../utils/area';
import styles from './Acesso.module.css';

/** Destino do link do e-mail "Esqueci minha senha": a pessoa define a nova senha (o e-mail e o código vêm na URL). */
export default function RedefinirSenha() {
  const [busca] = useSearchParams();
  const email = busca.get('email') ?? '';
  const token = busca.get('token') ?? '';

  const [nova, setNova] = useState('');
  const [repetida, setRepetida] = useState('');
  const [ver, setVer] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState('');
  const [feito, setFeito] = useState(false);

  const regras = {
    tamanho: nova.length >= 8,
    letraENumero: /[A-Za-z]/.test(nova) && /\d/.test(nova),
  };
  const confere = repetida.length > 0 && nova === repetida;
  const pode = regras.tamanho && regras.letraENumero && confere && !enviando;

  async function enviar(e: FormEvent) {
    e.preventDefault();
    if (!pode) return;
    setErro('');
    setEnviando(true);
    try {
      await redefinirSenha(email, token, nova);
      setFeito(true);
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível definir a nova senha.');
    } finally {
      setEnviando(false);
    }
  }

  const linkQuebrado = !email || !token;

  return (
    <div className={styles.centro}>
      <div className={styles.caixa}>
        <div className={styles.marca}>
          <b>SisMaster</b>
          <p>{AREA_ARBITROS ? 'Área do árbitro' : 'Gestão de campeonatos de basquete'}</p>
        </div>

        <div className={styles.card}>
          <h2>Definir nova senha</h2>

          {feito ? (
            <>
              <div className={`${styles.aviso} ${styles.info}`} role="status">Senha alterada. Entre com a nova senha.</div>
              <Link className={styles.botao} to="/entrar" style={{ display: 'block', textAlign: 'center', textDecoration: 'none' }}>Entrar</Link>
            </>
          ) : linkQuebrado ? (
            <>
              <div className={`${styles.aviso} ${styles.erro}`} role="alert">Este link está incompleto. Abra o link do e-mail inteiro ou peça um novo.</div>
              <p className={styles.rodape}><Link to="/entrar">Voltar ao login</Link></p>
            </>
          ) : (
            <form onSubmit={enviar} noValidate>
              {erro && <div className={`${styles.aviso} ${styles.erro}`} role="alert">{erro}</div>}
              <div className={styles.campo}>
                <label htmlFor="email">E-mail</label>
                <input id="email" type="email" value={email} readOnly />
              </div>
              <div className={styles.campo}>
                <label htmlFor="nova">Nova senha</label>
                <div className={styles.senha}>
                  <input id="nova" type={ver ? 'text' : 'password'} autoComplete="new-password" value={nova} onChange={e => setNova(e.target.value)} autoFocus />
                  <button type="button" onClick={() => setVer(v => !v)}>{ver ? 'Ocultar' : 'Mostrar'}</button>
                </div>
                <ul className={styles.regras}>
                  <li className={regras.tamanho ? styles.ok : ''}>Pelo menos 8 caracteres</li>
                  <li className={regras.letraENumero ? styles.ok : ''}>Letras e números</li>
                </ul>
              </div>
              <div className={styles.campo}>
                <label htmlFor="repetida">Repita a nova senha</label>
                <input id="repetida" type={ver ? 'text' : 'password'} autoComplete="new-password" value={repetida} onChange={e => setRepetida(e.target.value)} />
                {repetida.length > 0 && !confere && <span className={styles.erroCampo}>As senhas não são iguais.</span>}
              </div>
              <button className={styles.botao} disabled={!pode}>{enviando ? 'Salvando…' : 'Salvar nova senha'}</button>
              <p className={styles.rodape}><Link to="/entrar">Voltar ao login</Link></p>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}

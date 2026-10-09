import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { ROTULO_PERFIL, homeDoPerfil } from '../../auth/sessao';
import { AREA_ARBITROS } from '../../utils/area';
import styles from './Acesso.module.css';

/** Tela 16 — meus dados e troca de senha. Com senha temporária (1º acesso ou redefinição), a troca é obrigatória. */
export default function MinhaConta() {
  const { usuario, trocarSenha } = useAuth();
  const navigate = useNavigate();
  const [atual, setAtual] = useState('');
  const [nova, setNova] = useState('');
  const [repetida, setRepetida] = useState('');
  const [ver, setVer] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState('');
  const [feito, setFeito] = useState(false);

  if (!usuario) return null;
  const obrigatoria = usuario.trocarSenha;

  const regras = {
    tamanho: nova.length >= 8,
    letraENumero: /[A-Za-z]/.test(nova) && /\d/.test(nova),
    diferente: nova.length > 0 && nova !== atual,
  };
  const confere = repetida.length > 0 && nova === repetida;
  const pode = !!atual && regras.tamanho && regras.letraENumero && regras.diferente && confere && !enviando;

  async function trocar() {
    setErro('');
    setEnviando(true);
    try {
      const sessao = await trocarSenha(atual, nova);
      setAtual(''); setNova(''); setRepetida('');
      setFeito(true);
      if (obrigatoria) {
        // trocou a senha temporária: segue para a área do perfil
        const area = homeDoPerfil(sessao.usuario.perfil);
        const estaNaArea = AREA_ARBITROS ? area === '/arbitros/' : area === '/admin/';
        if (estaNaArea) navigate('/', { replace: true });
        else window.location.replace(area);
      }
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível trocar a senha.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className={styles.pagina}>
      <h1>Minha conta</h1>
      <p className={styles.sub}>Seus dados de acesso e a sua senha.</p>

      {obrigatoria && (
        <div className={`${styles.aviso} ${styles.atencao}`}>
          <b>Defina uma nova senha para continuar.</b> Você entrou com uma senha temporária. Até trocá-la, o resto do sistema fica bloqueado.
        </div>
      )}

      <div className={styles.card}>
        <h2>Dados</h2>
        <dl className={styles.dl}>
          <dt>Nome</dt><dd>{usuario.nome}</dd>
          <dt>E-mail (login)</dt><dd>{usuario.email}</dd>
          <dt>Perfil</dt><dd><span className={styles.selo}>{ROTULO_PERFIL[usuario.perfil]}</span></dd>
          {usuario.perfil !== 'Administrador' && (
            <><dt>Associações</dt><dd>{usuario.associacoes.length} vinculada{usuario.associacoes.length === 1 ? '' : 's'}</dd></>
          )}
        </dl>
        <p className={styles.dica} style={{ marginTop: 12 }}>Nome, e-mail e perfil são alterados por um administrador.</p>
      </div>

      <div className={styles.card}>
        <h2>Alterar senha</h2>
        {feito && !obrigatoria && <div className={`${styles.aviso} ${styles.ok}`}>Senha alterada.</div>}
        {erro && <div className={`${styles.aviso} ${styles.erro}`} role="alert">{erro}</div>}

        <div className={styles.campo}>
          <label htmlFor="atual">{obrigatoria ? 'Senha temporária recebida' : 'Senha atual'}</label>
          <div className={styles.senha}>
            <input id="atual" type={ver ? 'text' : 'password'} autoComplete="current-password" value={atual} onChange={e => setAtual(e.target.value)} />
            <button type="button" onClick={() => setVer(v => !v)}>{ver ? 'Ocultar' : 'Mostrar'}</button>
          </div>
        </div>
        <div className={styles.campo}>
          <label htmlFor="nova">Nova senha</label>
          <input id="nova" type={ver ? 'text' : 'password'} autoComplete="new-password" value={nova} onChange={e => setNova(e.target.value)} />
          <ul className={styles.regras}>
            <li className={regras.tamanho ? styles.ok : ''}>Pelo menos 8 caracteres</li>
            <li className={regras.letraENumero ? styles.ok : ''}>Letras e números</li>
            <li className={regras.diferente ? styles.ok : ''}>Diferente da senha atual</li>
          </ul>
        </div>
        <div className={styles.campo}>
          <label htmlFor="repetida">Repita a nova senha</label>
          <input id="repetida" type={ver ? 'text' : 'password'} autoComplete="new-password" value={repetida} onChange={e => setRepetida(e.target.value)} />
          {repetida.length > 0 && !confere && <span className={styles.erroCampo}>As senhas não conferem.</span>}
        </div>
        <button className={styles.botao} disabled={!pode} onClick={trocar}>{enviando ? 'Salvando…' : 'Alterar senha'}</button>
      </div>
    </div>
  );
}

import { useEffect, useRef, useState } from 'react';
import { Link, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { ROTULO_PERFIL } from '../../auth/sessao';
import { useThemeContext } from '../../contexts/ThemeContext';
import { AREA_ARBITROS } from '../../utils/area';
import acesso from '../../pages/Acesso/Acesso.module.css';
import styles from './AppLayout.module.css';

const iniciais = (nome: string) => nome.split(/\s+/).filter(Boolean).map(p => p[0]).slice(0, 2).join('').toUpperCase();

export default function AppLayout() {
  const { theme, toggle } = useThemeContext();
  const { usuario, sair } = useAuth();
  const navigate = useNavigate();
  const [aberto, setAberto] = useState(false);
  const caixa = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!aberto) return;
    const fora = (e: MouseEvent) => { if (!caixa.current?.contains(e.target as Node)) setAberto(false); };
    document.addEventListener('mousedown', fora);
    return () => document.removeEventListener('mousedown', fora);
  }, [aberto]);

  function deslogar() {
    sair();
    navigate('/entrar', { replace: true });
  }

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <span className={styles.logo}>{AREA_ARBITROS ? 'SisMaster · Árbitros' : 'SisMaster'}</span>
        <div className={acesso.direita}>
          <button
            className={styles.themeBtn}
            onClick={toggle}
            aria-label={theme === 'dark' ? 'Mudar para tema claro' : 'Mudar para tema escuro'}
          >
            {theme === 'dark' ? (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
                <line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
              </svg>
            ) : (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
              </svg>
            )}
            {theme === 'dark' ? 'Claro' : 'Escuro'}
          </button>

          {usuario && (
            <div className={acesso.usuario} ref={caixa}>
              <button className={acesso.usuarioBtn} onClick={() => setAberto(a => !a)} aria-label="Menu do usuário" aria-expanded={aberto}>
                <span className={acesso.avatar}>{iniciais(usuario.nome)}</span>
                <span>{usuario.nome}</span> ▾
              </button>
              {aberto && (
                <div className={acesso.menu} role="menu">
                  <div className={acesso.menuQuem}>{usuario.nome}<span>{ROTULO_PERFIL[usuario.perfil]}</span></div>
                  {!AREA_ARBITROS && (
                    <Link className={acesso.menuItem} to="/usuarios" onClick={() => setAberto(false)}>Usuários e perfis</Link>
                  )}
                  <Link className={acesso.menuItem} to="/minha-conta" onClick={() => setAberto(false)}>Minha conta e senha</Link>
                  <button className={acesso.menuItem} onClick={deslogar}>Sair</button>
                </div>
              )}
            </div>
          )}
        </div>
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  );
}

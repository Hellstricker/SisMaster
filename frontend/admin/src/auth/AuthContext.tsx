import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { AREA_ARBITROS } from '../utils/area';
import {
  type Sessao, type Usuario, aoMudarSessao, entrar as entrarNaApi, homeDoPerfil, lerSessao, renovarSessao,
  sair as sairDaApi, trocarSenha as trocarSenhaNaApi,
} from './sessao';

interface Contexto {
  sessao: Sessao | null;
  usuario: Usuario | null;
  entrar: (email: string, senha: string) => Promise<Sessao>;
  trocarSenha: (atual: string, nova: string) => Promise<Sessao>;
  sair: () => void;
}

const AuthContext = createContext<Contexto | null>(null);

export function useAuth(): Contexto {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth fora do AuthProvider');
  return ctx;
}

// Atividade do usuário: só quem está usando o sistema renova o token. Quem fica parado deixa a sessão vencer
// (30 min para administradores; 8 h para árbitros, que não podem cair no meio de um jogo).
let ultimaAtividade = Date.now();
const registrarAtividade = () => { ultimaAtividade = Date.now(); };

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sessao, setSessao] = useState<Sessao | null>(() => lerSessao());

  useEffect(() => aoMudarSessao(() => setSessao(lerSessao())), []);

  useEffect(() => {
    const eventos = ['pointerdown', 'keydown', 'touchstart'] as const;
    eventos.forEach(e => window.addEventListener(e, registrarAtividade, { passive: true }));
    const relogio = window.setInterval(() => {
      const atual = lerSessao();
      setSessao(prev => (prev && !atual ? null : prev)); // venceu: volta ao login pela guarda de rota
      if (!atual) return;
      const duracao = atual.expiraEm - atual.emitidoEm;
      const restante = atual.expiraEm - Date.now();
      const usandoHaPouco = Date.now() - ultimaAtividade < 5 * 60_000;
      if (restante < duracao * 0.6 && usandoHaPouco) void renovarSessao().catch(() => { /* tenta de novo no próximo ciclo */ });
    }, 30_000);
    return () => {
      eventos.forEach(e => window.removeEventListener(e, registrarAtividade));
      window.clearInterval(relogio);
    };
  }, []);

  const valor = useMemo<Contexto>(() => ({
    sessao,
    usuario: sessao?.usuario ?? null,
    entrar: entrarNaApi,
    trocarSenha: trocarSenhaNaApi,
    sair: sairDaApi,
  }), [sessao]);

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}

/** Tudo (menos o login) exige sessão. Quem ainda usa senha temporária só enxerga a tela de troca de senha. */
export function RequerLogin() {
  const { usuario } = useAuth();
  const local = useLocation();
  if (!usuario) return <Navigate to={`/entrar?voltar=${encodeURIComponent(local.pathname + local.search)}`} replace />;
  if (usuario.trocarSenha && local.pathname !== '/minha-conta') return <Navigate to="/minha-conta" replace />;
  return <Outlet />;
}

/** Gestão (/admin): o Árbitro não entra; é levado para a área dele. */
export function RequerGestao() {
  const { usuario } = useAuth();
  if (usuario?.perfil === 'Arbitro') {
    window.location.replace(homeDoPerfil('Arbitro'));
    return null;
  }
  return <Outlet />;
}

/** Quem já está logado e abre /entrar vai direto para a sua área. */
export function destinoAposEntrar(usuario: Usuario, voltar: string | null): { externo: boolean; caminho: string } {
  if (usuario.trocarSenha) return { externo: false, caminho: '/minha-conta' };
  const area = homeDoPerfil(usuario.perfil);
  const estouNaAreaCerta = AREA_ARBITROS ? area === '/arbitros/' || usuario.perfil !== 'Arbitro' : area === '/admin/';
  // só volta para a página anterior se ela for do mesmo app e a área servir ao perfil
  const seguro = voltar && voltar.startsWith('/') && !voltar.startsWith('//') ? voltar : null;
  if (!estouNaAreaCerta) return { externo: true, caminho: area };
  if (seguro) {
    const base = AREA_ARBITROS ? '/arbitros' : '/admin';
    return { externo: false, caminho: seguro.startsWith(base) ? seguro.slice(base.length) || '/' : '/' };
  }
  return { externo: false, caminho: '/' };
}

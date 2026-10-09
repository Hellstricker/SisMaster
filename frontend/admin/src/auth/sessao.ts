import { BASE, lerErro } from '../api/http';

/**
 * Sessão do usuário: o token que a Identidade emite fica no localStorage, para ser lido também pelas telas estáticas
 * da mesa (/arbitros/*.html), que são outras páginas da mesma origem.
 */
const CHAVE = 'sismaster.sessao';
const EVENTO = 'sismaster:sessao';

export type Perfil = 'Administrador' | 'AdministradorDeAssociacao' | 'Arbitro';

export const ROTULO_PERFIL: Record<Perfil, string> = {
  Administrador: 'Administrador',
  AdministradorDeAssociacao: 'Administrador de associação',
  Arbitro: 'Árbitro',
};

export interface Usuario {
  id: string;
  email: string;
  nome: string;
  perfil: Perfil;
  associacoes: string[];
  /** Verdadeiro enquanto a pessoa usa uma senha temporária: precisa trocá-la antes de qualquer outra coisa. */
  trocarSenha: boolean;
}

export interface Sessao {
  accessToken: string;
  /** Momento (ms) em que o token vence. */
  expiraEm: number;
  /** Momento (ms) em que foi emitido. */
  emitidoEm: number;
  usuario: Usuario;
}

interface RespostaLogin {
  accessToken: string;
  expiresIn: number;
  userToken: Usuario;
}

export function lerSessao(): Sessao | null {
  try {
    const bruto = localStorage.getItem(CHAVE);
    if (!bruto) return null;
    const s = JSON.parse(bruto) as Sessao;
    return Date.now() < s.expiraEm ? s : null;
  } catch {
    return null;
  }
}

function salvar(s: Sessao | null) {
  try {
    if (s) localStorage.setItem(CHAVE, JSON.stringify(s));
    else localStorage.removeItem(CHAVE);
  } catch { /* navegador sem armazenamento: a sessão vale só até recarregar */ }
  window.dispatchEvent(new Event(EVENTO));
}

/** Avisa quando a sessão muda (nesta aba ou em outra). Devolve a função que cancela. */
export function aoMudarSessao(fn: () => void): () => void {
  const deOutraAba = (e: StorageEvent) => { if (e.key === CHAVE) fn(); };
  window.addEventListener(EVENTO, fn);
  window.addEventListener('storage', deOutraAba);
  return () => { window.removeEventListener(EVENTO, fn); window.removeEventListener('storage', deOutraAba); };
}

function dessa(r: RespostaLogin): Sessao {
  const agora = Date.now();
  return { accessToken: r.accessToken, emitidoEm: agora, expiraEm: agora + r.expiresIn * 1000, usuario: r.userToken };
}

async function receber(res: Response, erroPadrao: string): Promise<Sessao> {
  if (!res.ok) throw await lerErro(res, erroPadrao);
  const sessao = dessa(await res.json() as RespostaLogin);
  salvar(sessao);
  return sessao;
}

const JSON_HEADERS = { 'Content-Type': 'application/json' };

export async function entrar(email: string, senha: string): Promise<Sessao> {
  const res = await fetch(`${BASE}/api/identidade/autenticar`, {
    method: 'POST', headers: JSON_HEADERS, body: JSON.stringify({ email, password: senha }),
  });
  return receber(res, 'Não foi possível entrar. Tente de novo.');
}

/** Troca o token por um novo (o fetch interceptado já manda o atual). Usado enquanto a pessoa está usando o sistema. */
export async function renovarSessao(): Promise<void> {
  const res = await fetch(`${BASE}/api/identidade/renovar`, { method: 'POST' });
  if (res.ok) await receber(res, '');
}

export async function trocarSenha(senhaAtual: string, novaSenha: string): Promise<Sessao> {
  const res = await fetch(`${BASE}/api/identidade/trocar-senha`, {
    method: 'POST', headers: JSON_HEADERS, body: JSON.stringify({ senhaAtual, novaSenha }),
  });
  return receber(res, 'Não foi possível trocar a senha.');
}

export function sair() {
  salvar(null);
}

/** Para onde cada perfil vai depois de entrar. */
export function homeDoPerfil(perfil: Perfil): string {
  return perfil === 'Arbitro' ? '/arbitros/' : '/admin/';
}

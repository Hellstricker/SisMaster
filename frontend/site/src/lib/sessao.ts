'use client';

/**
 * Conta do público no site: a chamada às rotas de identidade (mesma origem, pelo nginx) e a sessão guardada no navegador.
 * A sessão é o token JWT do Participante; vale poucos minutos e é renovada enquanto a pessoa usa o site.
 */

export interface UsuarioDoSite {
  id: string;
  email: string;
  nome: string;
  perfil: string | null;
}

export interface SessaoDoSite {
  accessToken: string;
  /** Instante (ms) em que o token vence. */
  expiraEm: number;
  usuario: UsuarioDoSite;
}

const CHAVE = 'sismaster.site.sessao';
const EVENTO = 'sismaster:sessao';

/** A mensagem que o login devolve para conta ainda não confirmada: a tela a reconhece para oferecer o reenvio. */
export const EMAIL_NAO_CONFIRMADO = 'Você ainda não confirmou o e-mail desta conta.';

export function lerSessao(): SessaoDoSite | null {
  try {
    const bruto = localStorage.getItem(CHAVE);
    if (!bruto) return null;
    const s = JSON.parse(bruto) as SessaoDoSite;
    if (!s.accessToken || s.expiraEm <= Date.now()) { localStorage.removeItem(CHAVE); return null; }
    return s;
  } catch {
    return null;
  }
}

function guardar(s: SessaoDoSite | null) {
  try {
    if (s) localStorage.setItem(CHAVE, JSON.stringify(s)); else localStorage.removeItem(CHAVE);
  } catch { /* navegador sem armazenamento: a sessão vale só nesta página */ }
  window.dispatchEvent(new Event(EVENTO));
}

export function aoMudarSessao(fn: () => void): () => void {
  window.addEventListener(EVENTO, fn);
  window.addEventListener('storage', fn);
  return () => { window.removeEventListener(EVENTO, fn); window.removeEventListener('storage', fn); };
}

export function sair() {
  guardar(null);
}

export type Resultado<T> = { ok: true; dados: T } | { ok: false; erro: string; status: number };

/** POST JSON para a Identidade; transforma as respostas de erro numa mensagem legível. */
export async function enviarParaIdentidade<T = unknown>(caminho: string, corpo: unknown): Promise<Resultado<T>> {
  try {
    const r = await fetch(`/api/identidade/${caminho}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
    if (r.ok) return { ok: true, dados: (await r.json().catch(() => ({}))) as T };
    if (r.status === 429) return { ok: false, status: 429, erro: 'Muitas tentativas em pouco tempo. Espere alguns minutos e tente de novo.' };
    const json = await r.json().catch(() => ({}));
    const lista = json?.errors?.messages ?? (json?.errors ? Object.values(json.errors).flat() : []);
    return { ok: false, status: r.status, erro: typeof lista?.[0] === 'string' ? lista[0] : 'Não foi possível concluir. Tente de novo.' };
  } catch {
    return { ok: false, status: 0, erro: 'Sem conexão com o servidor. Confira a internet e tente de novo.' };
  }
}

interface RespostaDeLogin {
  accessToken: string;
  expiresIn: number;
  userToken: { id: string; email: string; nome: string; perfil: string | null };
}

/** Guarda a sessão de uma resposta de login. Contas da equipe não entram pelo site: devolve o motivo. */
export function iniciarSessao(r: RespostaDeLogin): { ok: true; sessao: SessaoDoSite } | { ok: false; erro: string } {
  if (r.userToken.perfil !== 'Participante') {
    return { ok: false, erro: 'Esta conta é da equipe da associação. Entre pela área de gestão (/admin) ou de árbitros (/arbitros).' };
  }
  const sessao: SessaoDoSite = {
    accessToken: r.accessToken,
    expiraEm: Date.now() + r.expiresIn * 1000,
    usuario: { id: r.userToken.id, email: r.userToken.email, nome: r.userToken.nome, perfil: r.userToken.perfil },
  };
  guardar(sessao);
  return { ok: true, sessao };
}

export async function entrar(email: string, senha: string): Promise<{ ok: true; sessao: SessaoDoSite } | { ok: false; erro: string; naoConfirmado: boolean }> {
  const r = await enviarParaIdentidade<RespostaDeLogin>('autenticar', { email, password: senha });
  if (!r.ok) return { ok: false, erro: r.erro, naoConfirmado: r.erro.startsWith(EMAIL_NAO_CONFIRMADO) };
  const s = iniciarSessao(r.dados);
  return s.ok ? s : { ok: false, erro: s.erro, naoConfirmado: false };
}

/** Só caminhos internos do site: nunca um endereço de outro site (evita desvio depois do login). */
export function caminhoSeguro(voltar: string | null | undefined): string | null {
  return voltar && /^\/[A-Za-z0-9/_\-.]*$/.test(voltar) && !voltar.includes('//') ? voltar : null;
}

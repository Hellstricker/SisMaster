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
  /** Versão dos Termos e da Política aceita por último. Ausente em sessões antigas (o site renova o token para descobrir). */
  termosVersao?: string | null;
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
  userToken: { id: string; email: string; nome: string; perfil: string | null; termosVersao?: string | null };
}

/** Guarda a sessão de uma resposta de login. Contas da equipe não entram pelo site: devolve o motivo. */
export function iniciarSessao(r: RespostaDeLogin): { ok: true; sessao: SessaoDoSite } | { ok: false; erro: string } {
  if (r.userToken.perfil !== 'Participante') {
    return { ok: false, erro: 'Esta conta é da equipe da associação. Entre pela área de gestão (/admin) ou de árbitros (/arbitros).' };
  }
  const sessao: SessaoDoSite = {
    accessToken: r.accessToken,
    expiraEm: Date.now() + r.expiresIn * 1000,
    usuario: { id: r.userToken.id, email: r.userToken.email, nome: r.userToken.nome, perfil: r.userToken.perfil, termosVersao: r.userToken.termosVersao ?? null },
  };
  guardar(sessao);
  return { ok: true, sessao };
}

/** Pede um token novo (traz os dados atuais da conta, como a versão dos termos aceita). */
export async function atualizarSessao(): Promise<SessaoDoSite | null> {
  const atual = lerSessao();
  if (!atual) return null;
  try {
    const r = await fetch('/api/identidade/renovar', { method: 'POST', headers: { Authorization: `Bearer ${atual.accessToken}` } });
    if (!r.ok) return atual;
    const s = iniciarSessao((await r.json()) as RespostaDeLogin);
    return s.ok ? s.sessao : atual;
  } catch {
    return atual;
  }
}

/** A conta aceita a versão vigente dos Termos e da Política (depois de uma mudança relevante); a sessão passa a trazer a versão nova. */
export async function aceitarTermos(versao: string): Promise<{ ok: true } | { ok: false; erro: string }> {
  const r = await chamarApi<RespostaDeLogin>('POST', '/api/identidade/aceitar-termos', { versao });
  if (!r.ok) return { ok: false, erro: r.erro };
  const s = iniciarSessao(r.dados);
  return s.ok ? { ok: true } : { ok: false, erro: s.erro };
}

export async function entrar(email: string, senha: string): Promise<{ ok: true; sessao: SessaoDoSite } | { ok: false; erro: string; naoConfirmado: boolean }> {
  const r = await enviarParaIdentidade<RespostaDeLogin>('autenticar', { email, password: senha });
  if (!r.ok) return { ok: false, erro: r.erro, naoConfirmado: r.erro.startsWith(EMAIL_NAO_CONFIRMADO) };
  const s = iniciarSessao(r.dados);
  return s.ok ? s : { ok: false, erro: s.erro, naoConfirmado: false };
}

/**
 * Chamada autenticada à API (token da sessão). Renova o token quando falta pouco para vencer; resposta 401 encerra a sessão
 * (o chamador leva a pessoa a entrar de novo). Devolve também os erros já em texto legível.
 */
export async function chamarApi<T = unknown>(metodo: 'GET' | 'POST' | 'PUT', caminho: string, corpo?: unknown): Promise<Resultado<T> & { expirou?: boolean }> {
  let sessao = lerSessao();
  if (!sessao) return { ok: false, status: 401, erro: 'Sua sessão terminou. Entre de novo para continuar.', expirou: true };
  if (sessao.expiraEm - Date.now() < 5 * 60_000) {
    try {
      const r = await fetch('/api/identidade/renovar', { method: 'POST', headers: { Authorization: `Bearer ${sessao.accessToken}` } });
      if (r.ok) { const s = iniciarSessao((await r.json()) as RespostaDeLogin); if (s.ok) sessao = s.sessao; }
    } catch { /* segue com o token atual */ }
  }
  try {
    const r = await fetch(caminho, {
      method: metodo,
      headers: { Authorization: `Bearer ${sessao.accessToken}`, ...(corpo === undefined ? {} : { 'Content-Type': 'application/json' }) },
      body: corpo === undefined ? undefined : JSON.stringify(corpo),
    });
    if (r.status === 401) { sair(); return { ok: false, status: 401, erro: 'Sua sessão terminou. Entre de novo para continuar.', expirou: true }; }
    if (r.ok) return { ok: true, dados: (await r.json().catch(() => ({}))) as T };
    if (r.status === 429) return { ok: false, status: 429, erro: 'Muitas tentativas em pouco tempo. Espere um pouco e tente de novo.' };
    const json = await r.json().catch(() => ({}));
    const lista = json?.errors?.messages ?? [];
    return { ok: false, status: r.status, erro: typeof lista?.[0] === 'string' ? lista[0] : 'Não foi possível concluir. Tente de novo.' };
  } catch {
    return { ok: false, status: 0, erro: 'Sem conexão com o servidor. Confira a internet e tente de novo.' };
  }
}

/** Só caminhos internos do site: nunca um endereço de outro site (evita desvio depois do login). */
export function caminhoSeguro(voltar: string | null | undefined): string | null {
  return voltar && /^\/[A-Za-z0-9/_\-.]*$/.test(voltar) && !voltar.includes('//') ? voltar : null;
}

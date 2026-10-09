import { AREA_ARBITROS } from '../utils/area';
import { lerSessao, sair as limparSessao } from './sessao';

const EH_API = /^(https?:\/\/[^/]+)?\/api\//;
const EH_LOGIN = /\/api\/identidade\/autenticar/;

function urlDe(entrada: RequestInfo | URL): string {
  if (typeof entrada === 'string') return entrada;
  if (entrada instanceof URL) return entrada.href;
  return entrada.url;
}

/** Leva o usuário ao login, guardando a página para voltar a ela depois de entrar. */
export function irParaOLogin() {
  if (location.pathname.endsWith('/entrar')) return;
  const base = AREA_ARBITROS ? '/arbitros' : '/admin';
  location.href = `${base}/entrar?voltar=${encodeURIComponent(location.pathname + location.search)}`;
}

/**
 * Faz toda chamada a /api/ levar o token da sessão (um único ponto, em vez de mexer em cada arquivo de api/*.ts) e leva ao
 * login quando a API responde 401 (sessão vencida ou token inválido). O login em si não leva token.
 */
export function instalarFetchAutenticado() {
  const original = window.fetch.bind(window);
  window.fetch = async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const url = urlDe(entrada);
    const protegido = EH_API.test(url) && !EH_LOGIN.test(url);

    if (protegido) {
      const sessao = lerSessao();
      if (sessao) {
        const cabecalhos = new Headers(init?.headers ?? (entrada instanceof Request ? entrada.headers : undefined));
        cabecalhos.set('Authorization', `Bearer ${sessao.accessToken}`);
        init = { ...init, headers: cabecalhos };
      }
    }

    const resposta = await original(entrada, init);
    if (protegido && resposta.status === 401) {
      limparSessao();
      irParaOLogin();
    }
    return resposta;
  };
}

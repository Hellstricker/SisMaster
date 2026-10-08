export const BASE = import.meta.env.VITE_API_URL ?? '';

/** Extrai a primeira mensagem de erro da API (ValidationProblemDetails: errors.messages[]). */
export async function lerErro(res: Response, padrao: string): Promise<Error> {
  const json = await res.json().catch(() => ({}));
  const errors = json?.errors ?? json?.Errors;
  const lista: unknown = Array.isArray(errors) ? errors : errors?.messages ?? (errors ? Object.values(errors).flat() : []);
  const primeira = Array.isArray(lista) ? lista[0] : undefined;
  return new Error(typeof primeira === 'string' ? primeira : padrao);
}

export async function enviar(
  metodo: 'POST' | 'PATCH' | 'PUT' | 'DELETE',
  caminho: string,
  corpo: unknown,
  erroPadrao: string,
): Promise<void> {
  const res = await fetch(`${BASE}${caminho}`, {
    method: metodo,
    headers: { 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  if (!res.ok) throw await lerErro(res, erroPadrao);
}

export async function obter<T>(caminho: string, erroPadrao: string): Promise<T> {
  const res = await fetch(`${BASE}${caminho}`);
  if (!res.ok) throw await lerErro(res, erroPadrao);
  const json = await res.json();
  // Só desembrulha um envelope { data }; um objeto com campo "data" próprio (ex.: a data do jogo) é devolvido inteiro.
  const envelope = json && typeof json === "object" && !Array.isArray(json) && Object.keys(json).length === 1 && "data" in json;
  return (envelope ? json.data : json) as T;
}

/** Todas as mensagens de erro da API (ValidationProblemDetails: errors.messages[]); vazio se não houver nenhuma. */
export async function lerMensagens(res: Response): Promise<string[]> {
  const json = await res.json().catch(() => ({}));
  const errors = json?.errors ?? json?.Errors;
  const lista: unknown = Array.isArray(errors) ? errors : errors?.messages ?? (errors ? Object.values(errors).flat() : []);
  return Array.isArray(lista) ? lista.filter((m): m is string => typeof m === 'string') : [];
}

/** Envia e devolve o corpo da resposta; quando a API recusa, lança <see cref="ErroDaApi"/> com todas as mensagens. */
export async function enviarComResposta<T>(
  metodo: 'POST' | 'PUT' | 'PATCH',
  caminho: string,
  corpo: unknown,
  erroPadrao: string,
): Promise<T> {
  const res = await fetch(`${BASE}${caminho}`, {
    method: metodo,
    headers: { 'Content-Type': 'application/json' },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  if (!res.ok) {
    const mensagens = await lerMensagens(res);
    throw new ErroDaApi(mensagens.length ? mensagens : [erroPadrao]);
  }
  return (await res.json()) as T;
}

/** Erro da API com todas as mensagens (a primeira é a que `Error.message` mostra). */
export class ErroDaApi extends Error {
  readonly mensagens: string[];
  constructor(mensagens: string[]) {
    super(mensagens[0]);
    this.mensagens = mensagens;
  }
}

import { enviar, obter } from './http';
const BASE = import.meta.env.VITE_API_URL ?? '';

export interface Associacao {
  id: string;
  nome: string;
  sigla: string;
  uf: string;
  ativa: boolean;
  totalCampeonatos: number;
}

export interface Campeonato {
  id: string;
  nome: string;
}

export interface AssociacaoDetalhe {
  id: string;
  nome: string;
  sigla: string;
  uf: string;
  ativa: boolean;
  campeonatos: Campeonato[];
}

export async function listarAssociacoes(): Promise<Associacao[]> {
  const res = await fetch(`${BASE}/api/associacoes`);
  if (!res.ok) throw new Error('Erro ao carregar associações');
  const json = await res.json();
  return json.data ?? json;
}

export async function criarAssociacao(body: { nome: string; sigla: string; uf: string }): Promise<void> {
  const res = await fetch(`${BASE}/api/associacoes`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err?.errors?.[0] ?? 'Erro ao criar associação');
  }
}

export async function alterarStatusAssociacao(id: string, ativa: boolean): Promise<void> {
  // Só o Administrador altera: a recusa (403) volta com a mensagem da API em vez de falhar calada.
  return enviar('PATCH', `/api/associacoes/${id}/status`, { ativa }, 'Erro ao alterar o status da associação');
}

export async function obterAssociacao(id: string): Promise<AssociacaoDetalhe> {
  const res = await fetch(`${BASE}/api/associacoes/${id}`);
  if (!res.ok) throw new Error('Associação não encontrada');
  const json = await res.json();
  return json.data ?? json;
}

export async function editarAssociacao(id: string, body: { nome: string; sigla: string; uf: string }): Promise<void> {
  const res = await fetch(`${BASE}/api/associacoes/${id}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err?.errors?.[0] ?? 'Erro ao editar associação');
  }
}

export async function criarCampeonato(associacaoId: string, body: { nome: string }): Promise<void> {
  const res = await fetch(`${BASE}/api/associacoes/${associacaoId}/campeonatos`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err?.errors?.[0] ?? 'Erro ao criar campeonato');
  }
}

export interface PaginaPublicaDados {
  missao: string;
  quemSomos: string;
  gestaoDaDiretoria: string;
  whatsapp: string;
  instagram: string;
  email: string;
  ondeJogamos: string;
  diretoria: { cargo: string; nome: string }[];
}

export interface PaginaPublicaApi {
  missao: string | null;
  quemSomos: string | null;
  gestaoDaDiretoria: string | null;
  whatsapp: string | null;
  instagram: string | null;
  email: string | null;
  ondeJogamos: string | null;
  diretoria: { cargo: string; nome: string | null }[];
}

/** O conteúdo das páginas "A associação" e "Contato" do site (vazio se ainda não preenchido). */
export function obterPaginaPublica(associacaoId: string): Promise<PaginaPublicaApi> {
  return obter<PaginaPublicaApi>(`/api/associacoes/${associacaoId}/pagina-publica`, 'Erro ao carregar a página pública');
}

/** Grava o conteúdo (substitui o anterior). Administrador ou Administrador da associação. */
export function salvarPaginaPublica(associacaoId: string, d: PaginaPublicaDados): Promise<void> {
  return enviar('PUT', `/api/associacoes/${associacaoId}/pagina-publica`, d, 'Erro ao salvar a página pública');
}

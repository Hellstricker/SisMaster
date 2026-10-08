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
  const res = await fetch(`${BASE}/api/associacoes/${id}/status`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ativa }),
  });
  if (!res.ok) throw new Error('Erro ao alterar status');
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

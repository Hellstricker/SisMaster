const BASE = import.meta.env.VITE_API_URL ?? '';

export type StatusTemporada =
  | 'InscricoesAbertas'
  | 'InscricoesEncerradas'
  | 'EmAndamento'
  | 'Encerrada';

export interface Temporada {
  id: string;
  ano: number;
  status: StatusTemporada;
  dataInicioInscricoes: string;
  dataFimInscricoes: string;
}

export interface CampeonatoDetalhe {
  id: string;
  nome: string;
  associacaoId: string;
  temporadas: Temporada[];
}

export async function obterCampeonato(id: string): Promise<CampeonatoDetalhe> {
  const res = await fetch(`${BASE}/api/campeonatos/${id}`);
  if (!res.ok) throw new Error('Campeonato não encontrado');
  const json = await res.json();
  return json.data ?? json;
}

export async function editarCampeonato(campeonatoId: string, body: { nome: string }): Promise<void> {
  const res = await fetch(`${BASE}/api/campeonatos/${campeonatoId}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err?.errors?.[0] ?? err?.errors?.messages?.[0] ?? 'Erro ao editar o campeonato');
  }
}

export async function criarTemporada(
  campeonatoId: string,
  body: { ano: number; dataInicioInscricoes: string; dataFimInscricoes: string }
): Promise<void> {
  const res = await fetch(`${BASE}/api/campeonatos/${campeonatoId}/temporadas`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err?.errors?.[0] ?? 'Erro ao criar temporada');
  }
}

export async function encerrarInscricoes(campeonatoId: string, temporadaId: string): Promise<void> {
  const res = await fetch(
    `${BASE}/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar-inscricoes`,
    { method: 'PATCH' }
  );
  if (!res.ok) throw new Error('Erro ao encerrar inscrições');
}

export async function iniciarTemporada(campeonatoId: string, temporadaId: string): Promise<void> {
  const res = await fetch(
    `${BASE}/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/iniciar`,
    { method: 'PATCH' }
  );
  if (!res.ok) throw new Error('Erro ao iniciar temporada');
}

export async function encerrarTemporada(campeonatoId: string, temporadaId: string): Promise<void> {
  const res = await fetch(
    `${BASE}/api/campeonatos/${campeonatoId}/temporadas/${temporadaId}/encerrar`,
    { method: 'PATCH' }
  );
  if (!res.ok) throw new Error('Erro ao encerrar temporada');
}

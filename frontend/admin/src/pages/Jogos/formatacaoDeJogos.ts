import type { JogoLista, StatusJogo } from '../../api/jogos';

export const STATUS_ROTULO: Record<StatusJogo, string> = {
  Agendado: 'Agendado', EmAndamento: 'Em andamento', Encerrado: 'Encerrado', WO: 'W.O.', Dispensado: 'Dispensado',
};

const DIAS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb'];

export const rotuloLocal = (l: { nome: string; cidade: string; estado: string | null }) => `${l.nome} — ${l.cidade}${l.estado ? `/${l.estado}` : ''}`;
export const horaCurta = (h: string | null) => (h ? h.slice(0, 5) : '');

export function rotuloDia(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  return `${DIAS[new Date(y, m - 1, d).getDay()]}, ${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}/${y}`;
}

export const dataBrasileira = (iso: string | null) => (iso ? iso.split('-').reverse().join('/') : '');

export const textoDoPlacar = (j: JogoLista) =>
  j.status === 'Encerrado' && j.placarCasa != null && j.placarVisitante != null ? `${j.placarCasa} x ${j.placarVisitante}` : '';

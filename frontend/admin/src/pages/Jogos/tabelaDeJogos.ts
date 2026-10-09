import type { JogoLista } from '../../api/jogos';
import { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import { STATUS_ROTULO, dataBrasileira, horaCurta, rotuloLocal, textoDoPlacar } from './formatacaoDeJogos';

export interface OpcoesDaTabela {
  categoria: string;
  local: string;
  status: string;
  /** dd/mm/aaaa (ou vazio) */
  de: string;
  ate: string;
  agrupar: 'data' | 'local';
  placar: boolean;
  semData: boolean;
}

export const OPCOES_PADRAO: OpcoesDaTabela = { categoria: '', local: '', status: '', de: '', ate: '', agrupar: 'data', placar: true, semData: false };

/** Os jogos que entram na impressão ou no CSV, por data, hora e número. */
export function jogosDaTabela(jogos: JogoLista[], o: OpcoesDaTabela): JogoLista[] {
  const de = dataValida(o.de, 2000, 2100) ? ddmmParaIso(o.de) : null;
  const ate = dataValida(o.ate, 2000, 2100) ? ddmmParaIso(o.ate) : null;
  return jogos
    .filter(j =>
      (!o.categoria || j.categoria.id === o.categoria) &&
      (!o.local || j.local?.id === o.local) &&
      (!o.status || j.status === o.status) &&
      (j.data ? (!de || j.data >= de) && (!ate || j.data <= ate) : o.semData))
    .sort((a, b) => (a.data ?? '9').localeCompare(b.data ?? '9') || (a.hora ?? '').localeCompare(b.hora ?? '') || a.numero - b.numero);
}

const protegido = (v: string) => (/^[=+\-@]/.test(v) ? `'${v}` : v);
const celula = (v: string | number | null | undefined) => `"${protegido(String(v ?? '')).replace(/"/g, '""')}"`;

/** CSV para planilha (separador ";", com BOM para o Excel). Sem dado pessoal de atleta. */
export function montarCsv(jogos: JogoLista[], placar: boolean): string {
  const cabecalho = ['Nº', 'Data', 'Hora', 'Local', 'Categoria', 'Fase', 'Rodada', 'Casa', 'Visitante', 'Status', 'Placar'];
  const linhas = jogos.map(j => [
    j.numero, dataBrasileira(j.data), horaCurta(j.hora), j.local ? rotuloLocal(j.local) : '', j.categoria.nome, j.fase.nome, j.etiqueta,
    j.casa.texto, j.visitante.texto, STATUS_ROTULO[j.status], placar ? textoDoPlacar(j) : '',
  ]);
  return '﻿' + [cabecalho, ...linhas].map(l => l.map(celula).join(';')).join('\r\n');
}

export function baixar(nome: string, texto: string) {
  const url = URL.createObjectURL(new Blob([texto], { type: 'text/csv;charset=utf-8' }));
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

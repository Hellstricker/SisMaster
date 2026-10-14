'use client';

import { useEffect, useId, useRef, useState } from 'react';
import { dataParaIso, mascaraData } from '@/lib/inscricao';

const MESES = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
const DIAS = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

interface Props {
  id: string;
  /** DD/MM/AAAA */
  valor: string;
  aoMudar: (v: string) => void;
  anoMin?: number;
  anoMax?: number;
  /** Ano que o calendário mostra quando o campo está vazio (data de nascimento: um ano "no meio"). */
  anoInicial?: number;
}

/**
 * Campo de data com calendário em português (nunca o seletor nativo do navegador). Dá para digitar (a máscara põe as barras) ou
 * escolher no calendário, com mês e ano em listas para chegar rápido a uma data de nascimento.
 */
export default function CampoData({ id, valor, aoMudar, anoMin = 1900, anoMax = new Date().getFullYear(), anoInicial = 1975 }: Props) {
  const [aberto, setAberto] = useState(false);
  const [ref, setRef] = useState<{ ano: number; mes: number }>({ ano: anoInicial, mes: 0 });
  const caixa = useRef<HTMLDivElement>(null);
  const idCalendario = useId();

  // Ao abrir, o calendário mostra o mês da data digitada (se houver).
  function abrir() {
    const iso = dataParaIso(valor);
    if (iso) setRef({ ano: Number(iso.slice(0, 4)), mes: Number(iso.slice(5, 7)) - 1 });
    setAberto(true);
  }

  useEffect(() => {
    if (!aberto) return;
    const fora = (e: MouseEvent) => { if (caixa.current && !caixa.current.contains(e.target as Node)) setAberto(false); };
    const tecla = (e: KeyboardEvent) => { if (e.key === 'Escape') setAberto(false); };
    document.addEventListener('mousedown', fora);
    document.addEventListener('keydown', tecla);
    return () => { document.removeEventListener('mousedown', fora); document.removeEventListener('keydown', tecla); };
  }, [aberto]);

  const primeiroDia = new Date(ref.ano, ref.mes, 1).getDay();
  const diasNoMes = new Date(ref.ano, ref.mes + 1, 0).getDate();
  const hoje = new Date();
  const selecionada = dataParaIso(valor);
  const anos = Array.from({ length: anoMax - anoMin + 1 }, (_, k) => anoMax - k);

  function irMes(delta: number) {
    const d = new Date(ref.ano, ref.mes + delta, 1);
    if (d.getFullYear() < anoMin || d.getFullYear() > anoMax) return;
    setRef({ ano: d.getFullYear(), mes: d.getMonth() });
  }

  function escolher(dia: number) {
    aoMudar(`${String(dia).padStart(2, '0')}/${String(ref.mes + 1).padStart(2, '0')}/${ref.ano}`);
    setAberto(false);
  }

  return (
    <div className="dt" ref={caixa}>
      <div className="dt-linha">
        <input id={id} type="text" inputMode="numeric" placeholder="DD/MM/AAAA" autoComplete="bday" value={valor}
          onChange={e => aoMudar(mascaraData(e.target.value))} aria-haspopup="dialog" aria-expanded={aberto} aria-controls={idCalendario} />
        <button type="button" className="dt-abrir" aria-label="Abrir o calendário" onClick={() => (aberto ? setAberto(false) : abrir())}>📅</button>
      </div>
      {aberto && (
        <div className="cal" id={idCalendario} role="dialog" aria-label="Calendário">
          <header>
            <button type="button" aria-label="Mês anterior" onClick={() => irMes(-1)}>‹</button>
            <select aria-label="Mês" value={ref.mes} onChange={e => setRef(r => ({ ...r, mes: Number(e.target.value) }))}>
              {MESES.map((m, i) => <option key={m} value={i}>{m}</option>)}
            </select>
            <select aria-label="Ano" value={ref.ano} onChange={e => setRef(r => ({ ...r, ano: Number(e.target.value) }))}>
              {anos.map(a => <option key={a} value={a}>{a}</option>)}
            </select>
            <button type="button" aria-label="Próximo mês" onClick={() => irMes(1)}>›</button>
          </header>
          <div className="dias">
            {DIAS.map((d, i) => <span key={i} aria-hidden="true">{d}</span>)}
            {Array.from({ length: primeiroDia }, (_, i) => <span key={`v${i}`} />)}
            {Array.from({ length: diasNoMes }, (_, i) => {
              const dia = i + 1;
              const iso = `${ref.ano}-${String(ref.mes + 1).padStart(2, '0')}-${String(dia).padStart(2, '0')}`;
              const futuro = new Date(ref.ano, ref.mes, dia) > hoje;
              return (
                <button key={dia} type="button" disabled={futuro} className={iso === selecionada ? 'sel' : ''} aria-pressed={iso === selecionada} onClick={() => escolher(dia)}>{dia}</button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

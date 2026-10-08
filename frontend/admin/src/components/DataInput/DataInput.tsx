import { useEffect, useRef, useState } from 'react';
import styles from './DataInput.module.css';

const MESES = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro'];
const DIAS_SEMANA = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S'];

export function aplicarMascaraData(valor: string): string {
  const digits = valor.replace(/\D/g, '').slice(0, 8);
  if (digits.length <= 2) return digits;
  if (digits.length <= 4) return `${digits.slice(0, 2)}/${digits.slice(2)}`;
  return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
}

export function dataValida(ddmmaaaa: string, anoMin = 1900, anoMax = 2100): boolean {
  if (ddmmaaaa.length !== 10) return false;
  const [d, m, y] = ddmmaaaa.split('/').map(Number);
  if (!d || !m || !y || y < anoMin || y > anoMax) return false;
  const dt = new Date(y, m - 1, d);
  return dt.getDate() === d && dt.getMonth() === m - 1 && dt.getFullYear() === y;
}

export function ddmmParaIso(ddmmaaaa: string): string {
  const [d, m, y] = ddmmaaaa.split('/');
  return `${y}-${m}-${d}`;
}

export function hojeDdmm(): string {
  const h = new Date();
  return `${String(h.getDate()).padStart(2, '0')}/${String(h.getMonth() + 1).padStart(2, '0')}/${h.getFullYear()}`;
}

interface Props {
  id: string;
  value: string;
  onChange: (v: string) => void;
  anoMin?: number;
  anoMax?: number;
}

/** Campo de data DD/MM/AAAA com calendário em pt-BR (nunca usar input type="date" nativo). */
export default function DataInput({ id, value, onChange, anoMin = 1900, anoMax = 2100 }: Props) {
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [aberto, setAberto] = useState(false);
  const [mes, setMes] = useState(() => {
    if (dataValida(value, anoMin, anoMax)) {
      const [, m, y] = value.split('/').map(Number);
      return new Date(y, m - 1, 1);
    }
    return new Date(new Date().getFullYear(), new Date().getMonth(), 1);
  });

  useEffect(() => {
    if (!aberto) return;
    function handler(e: MouseEvent) {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [aberto]);

  function selecionarDia(dia: number) {
    const d = String(dia).padStart(2, '0');
    const m = String(mes.getMonth() + 1).padStart(2, '0');
    onChange(`${d}/${m}/${mes.getFullYear()}`);
    setAberto(false);
  }

  function navMes(delta: number) {
    setMes(prev => new Date(prev.getFullYear(), prev.getMonth() + delta, 1));
  }

  const ano = mes.getFullYear();
  const mesIdx = mes.getMonth();
  const primeiroDia = new Date(ano, mesIdx, 1).getDay();
  const diasNoMes = new Date(ano, mesIdx + 1, 0).getDate();
  const hoje = new Date();
  const [dSel, mSel, ySel] = dataValida(value, anoMin, anoMax) ? value.split('/').map(Number) : [0, 0, 0];

  return (
    <div ref={wrapperRef} className={styles.wrapper}>
      <input
        id={id}
        type="text"
        inputMode="numeric"
        className={styles.input}
        placeholder="DD/MM/AAAA"
        maxLength={10}
        value={value}
        onChange={e => onChange(aplicarMascaraData(e.target.value))}
      />
      <button
        type="button"
        className={styles.btnCalendario}
        onMouseDown={e => e.stopPropagation()}
        onClick={e => { e.stopPropagation(); setAberto(p => !p); }}
        tabIndex={-1}
        aria-label="Abrir calendário"
      >
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
          <line x1="16" y1="2" x2="16" y2="6" />
          <line x1="8" y1="2" x2="8" y2="6" />
          <line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      </button>

      {aberto && (
        <div className={styles.calendario} onMouseDown={e => e.stopPropagation()}>
          <div className={styles.calHeader}>
            <button type="button" className={styles.calNav} onClick={() => navMes(-1)}>‹</button>
            <span className={styles.calMesAno}>{MESES[mesIdx]} {ano}</span>
            <button type="button" className={styles.calNav} onClick={() => navMes(1)}>›</button>
          </div>
          <div className={styles.calGrid}>
            {DIAS_SEMANA.map((d, i) => <span key={i} className={styles.calDiaSemana}>{d}</span>)}
            {Array.from({ length: primeiroDia }).map((_, i) => <span key={`v${i}`} />)}
            {Array.from({ length: diasNoMes }).map((_, i) => {
              const dia = i + 1;
              const selecionado = dia === dSel && mesIdx + 1 === mSel && ano === ySel;
              const ehHoje = dia === hoje.getDate() && mesIdx === hoje.getMonth() && ano === hoje.getFullYear();
              return (
                <button
                  key={dia}
                  type="button"
                  className={`${styles.calDia} ${selecionado ? styles.calDiaSel : ''} ${ehHoje && !selecionado ? styles.calDiaHoje : ''}`}
                  onClick={() => selecionarDia(dia)}
                >
                  {dia}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

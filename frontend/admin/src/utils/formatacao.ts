const LOCALE = 'pt-BR';

export function formatarData(iso: string): string {
  const [y, m, d] = iso.split('T')[0].split('-');
  return `${d}/${m}/${y}`;
}

export function formatarDataHora(iso: string): string {
  return new Date(iso).toLocaleString(LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatarMoeda(valor: number): string {
  return new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'BRL' }).format(valor);
}

export function formatarDecimal(valor: number, casas = 2): string {
  return new Intl.NumberFormat(LOCALE, { minimumFractionDigits: casas, maximumFractionDigits: casas }).format(valor);
}

export function formatarInteiro(valor: number): string {
  return new Intl.NumberFormat(LOCALE).format(valor);
}

export function aplicarMascaraCpf(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** (99) 99999-9999 para celular (11 dígitos) ou (99) 9999-9999 para fixo (10 dígitos). */
export function aplicarMascaraTelefone(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11);
  if (d.length === 0) return '';
  if (d.length <= 2) return `(${d}`;
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
}

/** Converte "1.234,56" ou "120,5" digitados em número; NaN se inválido. */
export function paraNumero(texto: string): number {
  const limpo = texto.trim().replace(/\./g, '').replace(',', '.');
  return limpo === '' ? NaN : Number(limpo);
}

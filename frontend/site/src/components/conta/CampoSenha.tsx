'use client';

import { useState } from 'react';

/** Quão boa é a senha (só para orientar): tamanho, letra, número e comprimento extra. 0 a 100. */
export function forcaDaSenha(v: string): number {
  return Math.min(100, (v.length >= 8 ? 40 : v.length * 5) + (/[A-Za-z]/.test(v) ? 20 : 0) + (/\d/.test(v) ? 20 : 0) + (v.length >= 12 ? 20 : 0));
}

/** Regra do servidor: mínimo 8 caracteres, com letra e número. */
export function senhaValida(v: string): boolean {
  return v.length >= 8 && /[A-Za-z]/.test(v) && /\d/.test(v);
}

export default function CampoSenha({ id, rotulo = 'Senha', valor, aoMudar, nova = false, autoComplete }: {
  id: string; rotulo?: string; valor: string; aoMudar: (v: string) => void; nova?: boolean; autoComplete?: string;
}) {
  const [ver, setVer] = useState(false);
  const forca = forcaDaSenha(valor);
  return (
    <div className="f">
      <label htmlFor={id}>{rotulo}</label>
      <div className="pwd">
        <input id={id} type={ver ? 'text' : 'password'} value={valor} onChange={e => aoMudar(e.target.value)} autoComplete={autoComplete ?? (nova ? 'new-password' : 'current-password')} />
        <button type="button" onClick={() => setVer(v => !v)} aria-label={ver ? 'Ocultar a senha' : 'Mostrar a senha'}>{ver ? 'Ocultar' : 'Mostrar'}</button>
      </div>
      {nova && (
        <>
          <div className="forca" aria-hidden="true"><i style={{ width: `${forca}%`, background: forca < 60 ? '#dc2626' : forca < 90 ? '#d97706' : '#16a34a' }} /></div>
          <span className="hint">Mínimo de 8 caracteres, com letra e número.</span>
        </>
      )}
    </div>
  );
}

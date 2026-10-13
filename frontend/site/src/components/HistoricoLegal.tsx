import type { LegalVigente } from '@/lib/api';
import { dataLegal } from '@/lib/legal';

/** Rodapé dos textos legais: a última mudança e o histórico de versões (quem aceitou uma versão antiga pode conferir o que mudou). */
export default function HistoricoLegal({ legal }: { legal: LegalVigente }) {
  return (
    <>
      {legal.resumo && (
        <p className="aviso info"><b>O que mudou na versão {legal.versao}:</b> {legal.resumo}</p>
      )}
      <p className="muted small">Última atualização: {dataLegal(legal.vigenteDesde)} · Versão {legal.versao}</p>
      {legal.historico.length > 0 && (
        <details className="historico-legal">
          <summary>Versões anteriores</summary>
          <ul>
            {legal.historico.map(h => (
              <li key={h.versao}><b>{h.versao}</b> · {dataLegal(h.vigenteDesde)}{h.relevante ? ' · mudança relevante' : ''} — {h.resumo}</li>
            ))}
          </ul>
        </details>
      )}
    </>
  );
}

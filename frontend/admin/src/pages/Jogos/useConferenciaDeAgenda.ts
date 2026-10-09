import { useEffect, useState } from 'react';
import { type ItemDeAgenda, type ResultadoDaConferencia, conferirAgenda } from '../../api/jogos';

/**
 * Confere na API, enquanto o usuário preenche, os conflitos da agenda proposta (sem gravar). Bloqueios impedem salvar;
 * avisos só informam. A API confere de novo ao salvar, então uma falha aqui não deixa passar nada.
 */
export function useConferenciaDeAgenda(temporadaId: string | undefined, itens: ItemDeAgenda[] | null) {
  const chave = temporadaId && itens && itens.length > 0 ? JSON.stringify(itens) : '';
  const [estado, setEstado] = useState<{ chave: string; resultado: ResultadoDaConferencia } | null>(null);

  useEffect(() => {
    if (!chave || !temporadaId) return;
    let ativo = true;
    const espera = setTimeout(() => {
      conferirAgenda(temporadaId, JSON.parse(chave) as ItemDeAgenda[])
        .then(resultado => { if (ativo) setEstado({ chave, resultado }); })
        .catch(() => { /* a conferência é só um aviso antecipado; salvar confere de novo */ });
    }, 350);
    return () => { ativo = false; clearTimeout(espera); };
  }, [chave, temporadaId]);

  const atual = estado && estado.chave === chave ? estado.resultado : null;
  return { bloqueios: atual?.bloqueios ?? [], avisos: atual?.avisos ?? [], conferindo: chave !== '' && atual === null };
}

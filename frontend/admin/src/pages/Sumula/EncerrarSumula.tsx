import { useState } from 'react';
import { encerrarSumula } from '../../api/arbitros';
import base from '../Fases/Fases.module.css';

/** Botão "Encerrar súmula" com confirmação: o placar atual vira o resultado do jogo. */
export default function EncerrarSumula({ sumulaId, jogo, onEncerrada, pequeno = false }: {
  sumulaId: string; jogo: string; onEncerrada: () => void | Promise<void>; pequeno?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function confirmar() {
    setSalvando(true);
    setErro('');
    try {
      await encerrarSumula(sumulaId);
      setAberto(false);
      await onEncerrada();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível encerrar a súmula.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <button className={`${base.btnSecondary} ${pequeno ? base.btnSm : ''} ${base.btnPerigo}`} onClick={() => { setErro(''); setAberto(true); }}>
        Encerrar súmula
      </button>
      {aberto && (
        <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setAberto(false); }}>
          <div className={base.modal} style={{ maxWidth: 420 }}>
            <h2 className={base.modalTitulo}>Encerrar a súmula?</h2>
            <p className={base.dica} style={{ fontSize: 14 }}>
              <b>{jogo}</b><br />
              O placar atual vira o resultado do jogo, o jogo passa a Encerrado e a súmula não poderá mais ser alterada.
            </p>
            {erro && <p className={base.erroGlobal}>{erro}</p>}
            <div className={base.modalFooter}>
              <button className={base.btnSecondary} onClick={() => setAberto(false)} disabled={salvando}>Cancelar</button>
              <button className={`${base.btnPrimary} ${base.btnPerigoSolido}`} onClick={confirmar} disabled={salvando}>
                {salvando ? 'Encerrando...' : 'Encerrar súmula'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

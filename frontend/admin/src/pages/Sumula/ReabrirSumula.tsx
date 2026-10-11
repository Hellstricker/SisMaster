import { useState } from 'react';
import { reabrirSumula } from '../../api/arbitros';
import base from '../Fases/Fases.module.css';

/**
 * Botão "Reabrir súmula" para corrigir uma súmula já encerrada (árbitro ou gestão). O motivo é obrigatório e fica no histórico do
 * jogo; o jogo volta a Em andamento e o resultado sai da classificação. O servidor recusa se as fases seguintes já dependem dele.
 */
export default function ReabrirSumula({ sumulaId, jogo, onReaberta, pequeno = false }: {
  sumulaId: string; jogo: string; onReaberta: () => void | Promise<void>; pequeno?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function confirmar() {
    setSalvando(true);
    setErro('');
    try {
      await reabrirSumula(sumulaId, motivo.trim());
      setAberto(false);
      await onReaberta();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível reabrir a súmula.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <>
      <button className={`${base.btnSecondary} ${pequeno ? base.btnSm : ''}`} onClick={() => { setMotivo(''); setErro(''); setAberto(true); }}>
        Reabrir súmula…
      </button>
      {aberto && (
        <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setAberto(false); }}>
          <div className={base.modal} style={{ maxWidth: 440 }}>
            <h2 className={base.modalTitulo}>Reabrir a súmula?</h2>
            <p className={base.dica} style={{ fontSize: 14 }}><b>{jogo}</b></p>
            <p className={base.dica} style={{ fontSize: 13 }}>
              O jogo volta a <b>Em andamento</b>, o resultado sai da classificação e a súmula volta a aceitar correções. Depois é preciso encerrar de novo.
              Fica registrado no histórico do jogo.
            </p>
            <div className={base.field}>
              <label htmlFor="rs-motivo">Motivo <small>(obrigatório)</small></label>
              <textarea id="rs-motivo" className={base.input} maxLength={300} rows={3} autoFocus value={motivo}
                placeholder="Ex.: ponto de lance livre lançado na equipe errada" onChange={e => { setMotivo(e.target.value); setErro(''); }} />
            </div>
            {erro && <p className={base.erroGlobal} role="alert">{erro}</p>}
            <div className={base.modalFooter}>
              <button className={base.btnSecondary} onClick={() => setAberto(false)} disabled={salvando}>Cancelar</button>
              <button className={base.btnPrimary} onClick={confirmar} disabled={salvando || motivo.trim().length < 5}>
                {salvando ? 'Reabrindo...' : 'Reabrir súmula'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

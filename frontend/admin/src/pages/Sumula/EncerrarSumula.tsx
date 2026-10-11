import { useState } from 'react';
import { type SituacaoEncerramento, encerrarSumula, situacaoDoEncerramento } from '../../api/arbitros';
import base from '../Fases/Fases.module.css';

const PERIODO = ['', '1º período', '2º período', '3º período', '4º período', 'prorrogação'];
const relogio = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;

/**
 * Botão "Encerrar súmula" com confirmação: mostra o placar, o período e o tempo restante. Empate bloqueia (no basquete não há
 * empate: jogue a prorrogação); encerrar antes do fim do tempo só vale com a confirmação de que o jogo terminou assim mesmo.
 */
export default function EncerrarSumula({ sumulaId, jogo, onEncerrada, pequeno = false }: {
  sumulaId: string; jogo: string; onEncerrada: () => void | Promise<void>; pequeno?: boolean;
}) {
  const [aberto, setAberto] = useState(false);
  const [situacao, setSituacao] = useState<SituacaoEncerramento | null>(null);
  const [confirmaCedo, setConfirmaCedo] = useState(false);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function abrir() {
    setErro('');
    setSituacao(null);
    setConfirmaCedo(false);
    setAberto(true);
    try {
      setSituacao(await situacaoDoEncerramento(sumulaId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar a situação do jogo.');
    }
  }

  async function confirmar() {
    setSalvando(true);
    setErro('');
    try {
      await encerrarSumula(sumulaId, confirmaCedo);
      setAberto(false);
      await onEncerrada();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível encerrar a súmula.');
    } finally {
      setSalvando(false);
    }
  }

  const bloqueado = !situacao || situacao.empatado || (situacao.antesDoTempo && !confirmaCedo);

  return (
    <>
      <button className={`${base.btnSecondary} ${pequeno ? base.btnSm : ''} ${base.btnPerigo}`} onClick={abrir}>
        Encerrar súmula
      </button>
      {aberto && (
        <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setAberto(false); }}>
          <div className={base.modal} style={{ maxWidth: 440 }}>
            <h2 className={base.modalTitulo}>Encerrar a súmula?</h2>
            <p className={base.dica} style={{ fontSize: 14 }}><b>{jogo}</b></p>
            {situacao && (
              <>
                <p style={{ textAlign: 'center', fontSize: 15, margin: '10px 0 2px' }}>
                  {situacao.casa} <b style={{ fontSize: 28 }}>{situacao.placarCasa} × {situacao.placarVisitante}</b> {situacao.visitante}
                </p>
                <p className={base.dica} style={{ textAlign: 'center' }}>
                  {PERIODO[situacao.periodo] ?? `${situacao.periodo}º período`} · tempo restante {relogio(situacao.segundosRestantes)}
                  {situacao.cronometroAtivo ? ' (relógio correndo: será pausado)' : ''}
                </p>
                {situacao.empatado && (
                  <p className={base.erroGlobal} role="alert">Placar empatado: jogue a prorrogação antes de encerrar.</p>
                )}
                {!situacao.empatado && situacao.antesDoTempo && (
                  <div style={{ background: '#fffbeb', border: '1px solid #fcd34d', color: '#92400e', borderRadius: 8, padding: '9px 12px', fontSize: 13, margin: '10px 0' }}>
                    O jogo está no {PERIODO[situacao.periodo] ?? 'período'} com {relogio(situacao.segundosRestantes)} no relógio. Encerrar agora só vale se
                    o jogo terminou antes do tempo (desistência, lesão, queda de energia).
                    <label style={{ display: 'flex', gap: 8, marginTop: 8, alignItems: 'flex-start' }}>
                      <input type="checkbox" checked={confirmaCedo} onChange={e => setConfirmaCedo(e.target.checked)} />
                      <span>Confirmo que o jogo terminou assim mesmo.</span>
                    </label>
                  </div>
                )}
              </>
            )}
            <p className={base.dica} style={{ fontSize: 13 }}>
              O placar vira o resultado do jogo, a classificação é atualizada e as fases seguintes avançam. Depois, só reabrir a súmula (com motivo) permite corrigir.
            </p>
            {erro && <p className={base.erroGlobal}>{erro}</p>}
            <div className={base.modalFooter}>
              <button className={base.btnSecondary} onClick={() => setAberto(false)} disabled={salvando}>Cancelar</button>
              <button className={`${base.btnPrimary} ${base.btnPerigoSolido}`} onClick={confirmar} disabled={salvando || bloqueado}>
                {salvando ? 'Encerrando...' : 'Encerrar súmula'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

import type { ReactNode } from 'react';
import styles from './Inscricoes.module.css';

interface Props {
  titulo: string;
  largo?: boolean;
  erro?: string;
  salvando?: boolean;
  rotuloConfirmar?: string;
  onConfirmar: () => void;
  onFechar: () => void;
  children: ReactNode;
}

/** Casca padrão dos modais da tela de inscrições (overlay, título, erro e rodapé). */
export default function Modal({ titulo, largo, erro, salvando, rotuloConfirmar = 'Salvar', onConfirmar, onFechar, children }: Props) {
  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={`${styles.modal} ${largo ? styles.modalLargo : ''}`}>
        <h2 className={styles.modalTitulo}>{titulo}</h2>
        {children}
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button type="button" className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button type="button" className={styles.btnPrimary} onClick={onConfirmar} disabled={salvando}>
            {salvando ? 'Salvando...' : rotuloConfirmar}
          </button>
        </div>
      </div>
    </div>
  );
}

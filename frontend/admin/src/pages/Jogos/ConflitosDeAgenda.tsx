import styles from './Jogos.module.css';

export function AvisosDeAgenda({ bloqueios, avisos }: { bloqueios: string[]; avisos: string[] }) {
  return (
    <>
      {bloqueios.length > 0 && (
        <div className={styles.bloqueioAgenda} role="alert">
          <b>Não é possível salvar:</b>
          {bloqueios.map(b => <div key={b}>{b}</div>)}
        </div>
      )}
      {avisos.length > 0 && (
        <div className={styles.avisoLote}>
          <b>Atenção (pode salvar):</b>
          {avisos.map(a => <div key={a}>{a}</div>)}
        </div>
      )}
    </>
  );
}

import Link from 'next/link';
import { type CartaoDeJogo, diaDaSemana, diaMes, relogio, rotuloPeriodo } from '@/lib/api';

/** Cartão de jogo da vitrine: horário, placar (ou "W.O.") e, ao vivo, o período e o relógio. O vencedor fica em destaque. */
export default function CartaoJogo({ j, mostrarData = false }: { j: CartaoDeJogo; mostrarData?: boolean }) {
  const vivo = j.status === 'EmAndamento';
  const encerrado = j.status === 'Encerrado' || j.status === 'WO';
  const pc = vivo ? j.aoVivo?.placarCasa ?? j.placarCasa : j.placarCasa;
  const pv = vivo ? j.aoVivo?.placarVisitante ?? j.placarVisitante : j.placarVisitante;
  const casaVenceu = encerrado && pc != null && pv != null && pc > pv;
  const visitVenceu = encerrado && pc != null && pv != null && pv > pc;

  return (
    <Link href={`/jogos/${j.id}`} className={`card jogo-link ${vivo ? 'ao-vivo' : ''}`} style={{ margin: 0, display: 'block', textDecoration: 'none' }}>
      <div className="jogo">
        <div className={`eq ${encerrado && !casaVenceu ? 'perdeu' : ''}`}>{j.casa.texto}</div>
        <div className="centro">
          {vivo || encerrado ? (
            <span className="placar">{pc ?? 0} × {pv ?? 0}</span>
          ) : (
            <span className="hora">{j.hora ?? 'a definir'}</span>
          )}
        </div>
        <div className={`eq ${encerrado && !visitVenceu ? 'perdeu' : ''}`}>{j.visitante.texto}</div>
      </div>
      <div className="meta">
        {vivo && j.aoVivo && <span className="badge live">● {rotuloPeriodo(j.aoVivo.periodo)} · {relogio(j.aoVivo.segundosRestantes)}</span>}
        {j.status === 'WO' && <span className="badge wo">W.O.</span>}
        {encerrado && j.status !== 'WO' && <span>Encerrado</span>}
        {mostrarData && j.data && <span>{diaDaSemana(j.data)} {diaMes(j.data)}{j.hora ? ` · ${j.hora}` : ''}</span>}
        <span>{j.categoria} · {j.etiqueta}</span>
        {j.local && <span>📍 {j.local.nome}</span>}
      </div>
    </Link>
  );
}

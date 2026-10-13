import { useState } from 'react';
import type { DadosDesconto, Desconto, TemporadaCategoria, TipoDesconto } from '../../api/temporadas';
import DataInput, { dataValida, ddmmParaIso, hojeDdmm } from '../../components/DataInput/DataInput';
import { formatarMoeda, paraNumero } from '../../utils/formatacao';
import Modal from './Modal';
import styles from './Inscricoes.module.css';

type Confirmar<T> = (valor: T) => Promise<void>;

function useSalvar<T>(onConfirmar: Confirmar<T>) {
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  async function salvar(valor: T) {
    setSalvando(true);
    setErro('');
    try {
      await onConfirmar(valor);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
    } finally {
      setSalvando(false);
    }
  }
  return { erro, setErro, salvando, salvar };
}

// ---- Aprovar (com justificativa de exceção quando idade/sexo fogem do esperado)

export function ModalAprovar({ nome, foraDoEsperado, avisoDaDiretoria = false, onConfirmar, onFechar }: {
  nome: string; foraDoEsperado: string[]; avisoDaDiretoria?: boolean; onConfirmar: Confirmar<{ justificativa?: string; avisar: boolean }>; onFechar: () => void;
}) {
  const [justificativa, setJustificativa] = useState('');
  const [avisar, setAvisar] = useState(false);
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);
  const excecao = foraDoEsperado.length > 0;

  function confirmar() {
    if (excecao && !justificativa.trim()) { setErro('Informe a justificativa para aprovar mesmo assim.'); return; }
    salvar({ justificativa: excecao ? justificativa.trim() : undefined, avisar });
  }

  return (
    <Modal titulo={excecao ? 'Aprovar com exceção' : 'Aprovar inscrição'} erro={erro} salvando={salvando}
      rotuloConfirmar={excecao ? 'Aprovar mesmo assim' : 'Aprovar'} onConfirmar={confirmar} onFechar={onFechar}>
      <p className={styles.modalTexto}>{nome}</p>
      {excecao ? (
        <>
          <div className={styles.aviso}>Fora do esperado: {foraDoEsperado.join('; ')}.</div>
          <div className={styles.field}>
            <label htmlFor="just">Justificativa da diretoria</label>
            <textarea id="just" className={styles.textarea} rows={3} maxLength={500} value={justificativa}
              onChange={e => { setJustificativa(e.target.value); setErro(''); }} />
          </div>
        </>
      ) : (
        <p className={styles.modalTexto}>A pessoa seguirá para o pagamento da taxa de inscrição.</p>
      )}
      {avisoDaDiretoria && (
        <label className={styles.check}>
          <input id="av-aprovar" type="checkbox" checked={avisar} onChange={e => setAvisar(e.target.checked)} /> Avisar a pessoa por e-mail <small>(ficha feita pela diretoria: o e-mail do cadastro pode estar desatualizado)</small>
        </label>
      )}
    </Modal>
  );
}

// ---- Recusar (definitivo; o registro é mantido como histórico)

export function ModalRecusar({ nome, avisoDaDiretoria = false, onConfirmar, onFechar }: {
  nome: string; avisoDaDiretoria?: boolean; onConfirmar: Confirmar<{ motivo: string; avisar: boolean }>; onFechar: () => void;
}) {
  const [motivo, setMotivo] = useState('');
  const [avisar, setAvisar] = useState(false);
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);

  function confirmar() {
    if (!motivo.trim()) { setErro('Informe o motivo da recusa.'); return; }
    salvar({ motivo: motivo.trim(), avisar });
  }

  return (
    <Modal titulo="Recusar inscrição" erro={erro} salvando={salvando} rotuloConfirmar="Recusar" onConfirmar={confirmar} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        {nome}. A recusa é definitiva e fica no histórico; a pessoa poderá enviar uma nova inscrição.
      </p>
      <div className={styles.field}>
        <label htmlFor="motivo">Motivo da recusa</label>
        <textarea id="motivo" className={styles.textarea} rows={3} maxLength={500} value={motivo}
          onChange={e => { setMotivo(e.target.value); setErro(''); }} />
      </div>
      {avisoDaDiretoria && (
        <label className={styles.check}>
          <input id="av-recusar" type="checkbox" checked={avisar} onChange={e => setAvisar(e.target.checked)} /> Avisar a pessoa por e-mail <small>(ficha feita pela diretoria: o e-mail do cadastro pode estar desatualizado)</small>
        </label>
      )}
    </Modal>
  );
}

// ---- Valores das categorias da temporada (definidos depois do fim das inscrições)

export function ModalValoresCategorias({ categorias, onSalvar, onFechar }: {
  categorias: TemporadaCategoria[];
  onSalvar: (categoriaId: string, valor: number) => Promise<void>;
  onFechar: () => void;
}) {
  const [valores, setValores] = useState<Record<string, string>>(
    () => Object.fromEntries(categorias.map(c => [c.id, c.valor == null ? '' : String(c.valor).replace('.', ',')])));
  const [erro, setErro] = useState('');
  const [salvandoId, setSalvandoId] = useState<string | null>(null);

  async function salvar(c: TemporadaCategoria) {
    const n = paraNumero(valores[c.id] ?? '');
    if (Number.isNaN(n) || n < 0) { setErro(`Valor inválido para ${c.nome}.`); return; }
    setSalvandoId(c.id);
    setErro('');
    try { await onSalvar(c.categoriaId, n); } catch (e) { setErro(e instanceof Error ? e.message : 'Erro ao salvar.'); } finally { setSalvandoId(null); }
  }

  return (
    <Modal titulo="Valores das categorias" erro={erro} rotuloConfirmar="Concluir" onConfirmar={onFechar} onFechar={onFechar}>
      <p className={styles.modalTexto}>Definidos depois do fim das inscrições; usados na cobrança final de cada ficha.</p>
      {categorias.length === 0 && <p className={styles.mutado}>Nenhuma categoria na temporada.</p>}
      {categorias.map(c => (
        <div key={c.id} className={styles.valLinha}>
          <span className={styles.valNome}>{c.nome}</span>
          <div className={styles.field}>
            <label htmlFor={`val-${c.id}`}>Valor (R$)</label>
            <input id={`val-${c.id}`} className={styles.input} inputMode="decimal" value={valores[c.id] ?? ''}
              onChange={e => { setValores(p => ({ ...p, [c.id]: e.target.value })); setErro(''); }} />
          </div>
          <button type="button" className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={salvandoId === c.id} onClick={() => salvar(c)}>
            {salvandoId === c.id ? '...' : 'Salvar'}
          </button>
        </div>
      ))}
    </Modal>
  );
}

// ---- Cobrança da temporada: taxa fixa + descontos por combinação de categorias

export function ModalCobranca({ taxaAtual, descontos, categorias, onSalvarTaxa, onSalvarDesconto, onRemoverDesconto, onFechar }: {
  taxaAtual: number | null;
  descontos: Desconto[];
  categorias: TemporadaCategoria[];
  onSalvarTaxa: Confirmar<number | null>;
  onSalvarDesconto: Confirmar<DadosDesconto>;
  onRemoverDesconto: Confirmar<Desconto>;
  onFechar: () => void;
}) {
  const [taxa, setTaxa] = useState(taxaAtual == null ? '' : String(taxaAtual).replace('.', ','));
  const [escolhidas, setEscolhidas] = useState<string[]>([]);
  const [tipo, setTipo] = useState<TipoDesconto>('Percentual');
  const [valor, setValor] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function executar(fn: () => Promise<void>) {
    setSalvando(true);
    setErro('');
    try { await fn(); } catch (e) { setErro(e instanceof Error ? e.message : 'Erro ao salvar.'); } finally { setSalvando(false); }
  }

  function salvarTaxa() {
    const n = taxa.trim() === '' ? null : paraNumero(taxa);
    if (n !== null && (Number.isNaN(n) || n < 0)) { setErro('Taxa inválida.'); return; }
    executar(() => onSalvarTaxa(n));
  }

  function alternar(id: string) {
    setEscolhidas(p => (p.includes(id) ? p.filter(x => x !== id) : [...p, id]));
    setErro('');
  }

  // Combinação já configurada: carregar os valores atuais para editar.
  const existente = descontos.find(d => d.temporadaCategoriaIds.length === escolhidas.length && escolhidas.every(id => d.temporadaCategoriaIds.includes(id)));

  function salvarDesconto() {
    const v = paraNumero(valor);
    if (escolhidas.length < 2) { setErro('Escolha ao menos 2 categorias para a combinação.'); return; }
    if (Number.isNaN(v) || v < 0 || (tipo === 'Percentual' && v > 100)) { setErro('Valor do desconto inválido.'); return; }
    executar(async () => { await onSalvarDesconto({ temporadaCategoriaIds: escolhidas, tipo, valor: v }); setEscolhidas([]); setValor(''); });
  }

  function editar(d: Desconto) {
    setEscolhidas(d.temporadaCategoriaIds);
    setTipo(d.tipo);
    setValor(String(d.valor).replace('.', ','));
    setErro('');
  }

  return (
    <Modal titulo="Cobrança da temporada" largo erro={erro} salvando={salvando} rotuloConfirmar="Concluir" onConfirmar={onFechar} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        A taxa é fixa, paga uma vez por ficha ao ter a inscrição aceita. O desconto depende de <b>quais categorias</b> a pessoa joga:
        cada combinação tem o seu (ex.: M40+ e M55+ é diferente de M30+, M40+ e M55+). Combinação sem desconto cadastrado não recebe desconto.
      </p>

      <div className={styles.descLinha} style={{ gridTemplateColumns: '1fr auto' }}>
        <div className={styles.field}>
          <label htmlFor="taxa">Taxa de inscrição (R$)</label>
          <input id="taxa" className={styles.input} inputMode="decimal" placeholder="a definir" value={taxa}
            onChange={e => { setTaxa(e.target.value); setErro(''); }} />
        </div>
        <button type="button" className={styles.btnSecondary} onClick={salvarTaxa} disabled={salvando}>Salvar taxa</button>
      </div>

      <div className={styles.fieldset}>
        <span className={styles.legenda}>Descontos por combinação de categorias</span>
        {descontos.length === 0 ? (
          <p className={styles.mutado}>Nenhum desconto configurado.</p>
        ) : (
          <ul className={styles.descLista}>
            {descontos.map(d => (
              <li key={d.id}>
                <span>{d.categorias.join(' + ')}</span>
                <span className={styles.descAcoes}>
                  <b>{d.tipo === 'Percentual' ? `−${d.valor}%` : `−${formatarMoeda(d.valor)}`}</b>
                  <button type="button" className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => editar(d)} disabled={salvando}>Editar</button>
                  <button type="button" className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} onClick={() => executar(() => onRemoverDesconto(d))} disabled={salvando}>Remover</button>
                </span>
              </li>
            ))}
          </ul>
        )}

        <span className={styles.legenda} style={{ display: 'block', marginTop: 8 }}>Combinação (marque as categorias)</span>
        {categorias.length < 2 && <p className={styles.mutado}>Esta temporada tem {categorias.length === 1 ? 'só 1 categoria' : 'nenhuma categoria'}: para configurar descontos, vincule ao menos 2 categorias à temporada.</p>}
        <div className={styles.combo}>
          {categorias.map(c => (
            <label key={c.id} className={styles.check}>
              <input type="checkbox" checked={escolhidas.includes(c.id)} onChange={() => alternar(c.id)} /> {c.nome}
            </label>
          ))}
        </div>
        {existente && <p className={styles.mutado}>Esta combinação já tem desconto ({existente.tipo === 'Percentual' ? `${existente.valor}%` : formatarMoeda(existente.valor)}): salvar atualiza.</p>}

        <div className={styles.descLinha} style={{ gridTemplateColumns: '1fr 1fr auto' }}>
          <div className={styles.field}>
            <label htmlFor="d-tipo">Tipo</label>
            <select id="d-tipo" className={styles.select} value={tipo} onChange={e => setTipo(e.target.value as TipoDesconto)}>
              <option value="Percentual">Percentual (%)</option>
              <option value="Valor">Valor (R$)</option>
            </select>
          </div>
          <div className={styles.field}>
            <label htmlFor="d-valor">Desconto</label>
            <input id="d-valor" className={styles.input} inputMode="decimal" value={valor}
              onChange={e => { setValor(e.target.value); setErro(''); }} />
          </div>
          <button type="button" className={styles.btnSecondary} onClick={salvarDesconto} disabled={salvando || categorias.length < 2}>Salvar</button>
        </div>
      </div>
    </Modal>
  );
}

// ---- Pagamento (taxa ou saldo)

export function ModalPagamento({ tipo, nome, saldo, taxa, avisoDaDiretoria = false, onConfirmar, onFechar }: {
  tipo: 'taxa' | 'saldo';
  nome: string;
  saldo?: number;
  taxa?: number | null;
  avisoDaDiretoria?: boolean;
  onConfirmar: Confirmar<{ valor?: number; dataIso: string; avisar: boolean }>;
  onFechar: () => void;
}) {
  const [data, setData] = useState(hojeDdmm());
  const [avisar, setAvisar] = useState(false);
  const [valor, setValor] = useState(saldo != null ? String(saldo.toFixed(2)).replace('.', ',') : '');
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);

  function confirmar() {
    if (!dataValida(data)) { setErro('Informe uma data válida.'); return; }
    if (tipo === 'saldo') {
      const n = paraNumero(valor);
      if (Number.isNaN(n) || n <= 0) { setErro('Informe um valor válido.'); return; }
      if (saldo != null && n > saldo + 0.001) { setErro(`Valor maior que o saldo em aberto (${formatarMoeda(saldo)}).`); return; }
      salvar({ valor: n, dataIso: ddmmParaIso(data), avisar });
    } else {
      salvar({ dataIso: ddmmParaIso(data), avisar });
    }
  }

  return (
    <Modal titulo={tipo === 'taxa' ? 'Registrar taxa de inscrição' : 'Registrar pagamento do saldo'} erro={erro} salvando={salvando}
      rotuloConfirmar="Registrar" onConfirmar={confirmar} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        {nome}
        {tipo === 'taxa'
          ? taxa != null ? ` — taxa fixa de ${formatarMoeda(taxa)}. As categorias aprovadas da ficha serão efetivadas.` : ''
          : saldo != null ? ` — saldo em aberto ${formatarMoeda(saldo)}.` : ''}
      </p>
      {tipo === 'saldo' && (
        <div className={styles.field}>
          <label htmlFor="pg-valor">Valor pago (R$)</label>
          <input id="pg-valor" className={styles.input} inputMode="decimal" value={valor}
            onChange={e => { setValor(e.target.value); setErro(''); }} />
        </div>
      )}
      <div className={styles.field}>
        <label htmlFor="pg-data">Data do pagamento</label>
        <DataInput id="pg-data" value={data} onChange={v => { setData(v); setErro(''); }} anoMin={2000} anoMax={2100} />
      </div>
      {avisoDaDiretoria && tipo === 'taxa' && (
        <label className={styles.check}>
          <input id="av-pagamento" type="checkbox" checked={avisar} onChange={e => setAvisar(e.target.checked)} /> Avisar a pessoa por e-mail <small>(ficha feita pela diretoria: o e-mail do cadastro pode estar desatualizado)</small>
        </label>
      )}
    </Modal>
  );
}

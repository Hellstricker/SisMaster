import { useState } from 'react';
import type { Sexo } from '../../api/categorias';
import {
  type FiltroDeExportacao, type Ficha, type Identificacao, type PreviaDaImportacao, type ResultadoDaImportacao,
  exportarCsv, importarPlanilha, previaDaImportacao,
} from '../../api/inscricoes';
import DataInput, { dataValida, ddmmParaIso } from '../../components/DataInput/DataInput';
import { aplicarMascaraCpf, aplicarMascaraTelefone, formatarData, formatarMoeda } from '../../utils/formatacao';
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

const POSICOES = ['Armador', 'Ala-armador', 'Ala', 'Ala-pivô', 'Pivô'];
const isoParaDdmm = (iso: string) => formatarData(iso);

// ---- Corrigir dados da ficha (altura, peso, posição, plano e contato)

export interface DadosEditados {
  alturaCm: number | null;
  pesoKg: number | null;
  posicao: string | null;
  possuiPlanoSaude: boolean;
  nomePlanoSaude: string | null;
  email: string;
  telefone: string | null;
}

export function ModalEditarFicha({ ficha, onConfirmar, onFechar }: { ficha: Ficha; onConfirmar: Confirmar<DadosEditados>; onFechar: () => void }) {
  const [f, setF] = useState({
    alturaCm: ficha.dados.alturaCm?.toString() ?? '',
    pesoKg: ficha.dados.pesoKg?.toString() ?? '',
    posicao: ficha.dados.posicao ?? '',
    plano: ficha.dados.possuiPlanoSaude,
    nomePlano: ficha.dados.nomePlanoSaude ?? '',
    email: ficha.pessoa.email,
    telefone: aplicarMascaraTelefone(ficha.pessoa.telefone ?? ''),
  });
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);
  const campo = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => { setF(p => ({ ...p, [k]: v })); setErro(''); };

  function confirmar() {
    if (!f.email.includes('@')) return setErro('Informe um e-mail válido.');
    const tel = f.telefone.replace(/\D/g, '');
    if (tel && tel.length < 10) return setErro('Telefone incompleto: informe o DDD e o número.');
    if (f.plano && !f.nomePlano.trim()) return setErro('Informe o nome do plano de saúde.');
    salvar({
      alturaCm: f.alturaCm ? Number(f.alturaCm) : null,
      pesoKg: f.pesoKg ? Number(f.pesoKg) : null,
      posicao: f.posicao || null,
      possuiPlanoSaude: f.plano,
      nomePlanoSaude: f.plano ? f.nomePlano.trim() : null,
      email: f.email.trim(),
      telefone: tel || null,
    });
  }

  return (
    <Modal titulo="Corrigir dados da ficha" largo erro={erro} salvando={salvando} onConfirmar={confirmar} onFechar={onFechar}>
      <p className={styles.modalTexto}>{ficha.pessoa.nome}</p>
      <div className={styles.grid}>
        <div className={styles.field}><label htmlFor="e-alt">Altura (cm)</label>
          <input id="e-alt" className={styles.input} inputMode="numeric" value={f.alturaCm} onChange={e => campo('alturaCm', e.target.value.replace(/\D/g, '').slice(0, 3))} /></div>
        <div className={styles.field}><label htmlFor="e-peso">Peso (kg)</label>
          <input id="e-peso" className={styles.input} inputMode="numeric" value={f.pesoKg} onChange={e => campo('pesoKg', e.target.value.replace(/\D/g, '').slice(0, 3))} /></div>
        <div className={styles.field}><label htmlFor="e-pos">Posição</label>
          <select id="e-pos" className={styles.select} value={f.posicao} onChange={e => campo('posicao', e.target.value)}>
            <option value="">—</option>
            {POSICOES.map(p => <option key={p} value={p}>{p}</option>)}
          </select></div>
        <div className={styles.field}><label htmlFor="e-tel">Telefone</label>
          <input id="e-tel" className={styles.input} inputMode="tel" value={f.telefone} onChange={e => campo('telefone', aplicarMascaraTelefone(e.target.value))} /></div>
        <div className={`${styles.field} ${styles.cheio}`}><label htmlFor="e-email">E-mail</label>
          <input id="e-email" className={styles.input} type="email" value={f.email} onChange={e => campo('email', e.target.value)} /></div>
        <label className={`${styles.check} ${styles.cheio}`}>
          <input type="checkbox" checked={f.plano} onChange={e => campo('plano', e.target.checked)} /> Possui plano de saúde
        </label>
        {f.plano && (
          <div className={`${styles.field} ${styles.cheio}`}><label htmlFor="e-plano">Qual plano</label>
            <input id="e-plano" className={styles.input} value={f.nomePlano} onChange={e => campo('nomePlano', e.target.value)} /></div>
        )}
      </div>
      <p className={styles.modalTexto} style={{ marginTop: 12 }}>
        Nome, CPF, nascimento e sexo não se editam aqui: definem a elegibilidade nas categorias. Só o Administrador corrige esses quatro (opção “Corrigir identificação”).
      </p>
    </Modal>
  );
}

// ---- Corrigir identificação (só o Administrador; fica na auditoria)

export function ModalIdentificacao({ ficha, anoTemporada, onConfirmar, onFechar }: {
  ficha: Ficha; anoTemporada: number; onConfirmar: Confirmar<Identificacao>; onFechar: () => void;
}) {
  const [f, setF] = useState({ nome: ficha.pessoa.nome, cpf: ficha.pessoa.cpf, nascimento: isoParaDdmm(ficha.pessoa.nascimento), sexo: ficha.pessoa.sexo as Sexo });
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);
  const campo = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => { setF(p => ({ ...p, [k]: v })); setErro(''); };

  function confirmar() {
    if (f.nome.trim().length < 3) return setErro('Informe o nome completo.');
    if (f.cpf.replace(/\D/g, '').length !== 11) return setErro('CPF incompleto.');
    if (!dataValida(f.nascimento, 1900, anoTemporada)) return setErro('Data de nascimento inválida (DD/MM/AAAA).');
    salvar({ nome: f.nome.trim(), cpf: f.cpf, nascimento: ddmmParaIso(f.nascimento), sexo: f.sexo });
  }

  return (
    <Modal titulo="Corrigir identificação" largo erro={erro} salvando={salvando} onConfirmar={confirmar} onFechar={onFechar}>
      <div className={styles.aviso}>Mudar nascimento ou sexo pode deixar a pessoa fora do esperado numa categoria. A correção fica registrada (quem, quando e o que mudou; o CPF aparece mascarado).</div>
      <div className={styles.grid}>
        <div className={`${styles.field} ${styles.cheio}`}><label htmlFor="i-nome">Nome completo</label>
          <input id="i-nome" className={styles.input} value={f.nome} onChange={e => campo('nome', e.target.value)} /></div>
        <div className={styles.field}><label htmlFor="i-cpf">CPF</label>
          <input id="i-cpf" className={styles.input} inputMode="numeric" value={f.cpf} onChange={e => campo('cpf', aplicarMascaraCpf(e.target.value))} /></div>
        <div className={styles.field}><label htmlFor="i-nasc">Nascimento</label>
          <DataInput id="i-nasc" value={f.nascimento} onChange={v => campo('nascimento', v)} anoMin={1900} anoMax={anoTemporada} /></div>
        <div className={styles.field}><label htmlFor="i-sexo">Sexo</label>
          <select id="i-sexo" className={styles.select} value={f.sexo} onChange={e => campo('sexo', e.target.value as Sexo)}>
            <option value="Masculino">Masculino</option>
            <option value="Feminino">Feminino</option>
          </select></div>
      </div>
    </Modal>
  );
}

// ---- Unir cadastro duplicado (só o Administrador; fica na auditoria)

export function ModalUnir({ ficha, onConfirmar, onFechar }: { ficha: Ficha; onConfirmar: Confirmar<string>; onFechar: () => void }) {
  const [cpf, setCpf] = useState('');
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);

  function confirmar() {
    if (cpf.replace(/\D/g, '').length !== 11) return setErro('Informe o CPF completo do cadastro duplicado.');
    salvar(cpf);
  }

  return (
    <Modal titulo="Unir cadastro duplicado" largo erro={erro} salvando={salvando} rotuloConfirmar="Unir cadastros" onConfirmar={confirmar} onFechar={onFechar}>
      <p className={styles.modalTexto}>Cadastro que fica: <b>{ficha.pessoa.nome}</b> ({ficha.pessoa.cpf}).</p>
      <div className={styles.field}><label htmlFor="u-cpf">CPF do cadastro duplicado</label>
        <input id="u-cpf" className={styles.input} inputMode="numeric" value={cpf} onChange={e => { setCpf(aplicarMascaraCpf(e.target.value)); setErro(''); }} /></div>
      <div className={styles.aviso}>
        As fichas do cadastro duplicado e a conta do site dele (se houver) passam para <b>{ficha.pessoa.nome}</b>, e o cadastro duplicado é apagado.
        Os dados de {ficha.pessoa.nome} não mudam. Não dá para desfazer; a união fica registrada na auditoria (CPFs mascarados).
        Não une se os dois tiverem pedido ativo na mesma categoria ou contas diferentes.
      </div>
    </Modal>
  );
}

// ---- Pagamentos da ficha: histórico e estorno

export function ModalPagamentos({ ficha, somenteLeitura, onEstornar, onFechar }: {
  ficha: Ficha; somenteLeitura: boolean; onEstornar: (pagamentoId: string, motivo: string) => Promise<void>; onFechar: () => void;
}) {
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const fin = ficha.financeiro;

  async function estornar(id: string) {
    if (!motivo.trim()) return setErro('Informe o motivo do estorno antes de estornar.');
    setSalvando(true);
    setErro('');
    try { await onEstornar(id, motivo.trim()); } catch (e) { setErro(e instanceof Error ? e.message : 'Erro ao estornar.'); } finally { setSalvando(false); }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>Pagamentos da ficha</h2>
        <p className={styles.modalTexto}>
          {ficha.pessoa.nome} — pago {formatarMoeda(fin.totalPago)}{fin.cobrancaGerada ? ` de ${formatarMoeda(fin.valorFinal ?? 0)}` : ''}
        </p>
        {fin.pagamentos.length === 0 ? <p className={styles.modalTexto}>Nenhum pagamento lançado.</p> : (
          <ul className={styles.historico}>
            {fin.pagamentos.map(p => (
              <li key={p.id}>
                <span className={p.estornado ? styles.estornado : ''}>
                  <b>{p.tipo}</b> {formatarMoeda(p.valor)} <span className={styles.mutado}>em {formatarData(p.data)}</span>
                  {p.estornado && <span className={styles.mutado}> · estornado em {formatarData(p.estornadoEm!)}: {p.motivoEstorno}</span>}
                </span>
                {!p.estornado && !somenteLeitura && (
                  <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} disabled={salvando} onClick={() => estornar(p.id)}>Estornar</button>
                )}
              </li>
            ))}
          </ul>
        )}
        {!somenteLeitura && fin.pagamentos.some(p => !p.estornado) && (
          <div className={styles.field}>
            <label htmlFor="p-motivo">Motivo do estorno <small>(obrigatório)</small></label>
            <input id="p-motivo" className={styles.input} maxLength={300} placeholder="Ex.: lançado na ficha errada" value={motivo}
              onChange={e => { setMotivo(e.target.value); setErro(''); }} />
            <span className={styles.mutado}>O lançamento não é apagado: fica no histórico, riscado, com o motivo.</span>
          </div>
        )}
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}><button className={styles.btnSecondary} onClick={onFechar}>Fechar</button></div>
      </div>
    </div>
  );
}

// ---- Reabrir pedido recusado

export function ModalReabrir({ nome, motivoAnterior, onConfirmar, onFechar }: {
  nome: string; motivoAnterior: string | null; onConfirmar: Confirmar<void>; onFechar: () => void;
}) {
  const { erro, salvando, salvar } = useSalvar<void>(onConfirmar);
  return (
    <Modal titulo="Reabrir pedido recusado" erro={erro} salvando={salvando} rotuloConfirmar="Reabrir" onConfirmar={() => salvar()} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        O pedido de <b>{nome}</b> volta para <b>Pendente</b>. Depois é só aprovar ou recusar de novo.
        {motivoAnterior && <> Motivo da recusa anterior: “{motivoAnterior}” (fica no registro de auditoria).</>}
      </p>
    </Modal>
  );
}

// ---- Desfazer aprovação

export function ModalDesfazerAprovacao({ nome, onConfirmar, onFechar }: { nome: string; onConfirmar: Confirmar<string>; onFechar: () => void }) {
  const [motivo, setMotivo] = useState('');
  const { erro, setErro, salvando, salvar } = useSalvar(onConfirmar);
  return (
    <Modal titulo="Desfazer aprovação" erro={erro} salvando={salvando} rotuloConfirmar="Desfazer aprovação"
      onConfirmar={() => (motivo.trim() ? salvar(motivo.trim()) : setErro('Informe o motivo.'))} onFechar={onFechar}>
      <p className={styles.modalTexto}>
        O pedido de <b>{nome}</b> volta para <b>Pendente</b>. Se a pessoa já estiver numa equipe, primeiro tire-a da equipe. Os pagamentos lançados não são apagados.
      </p>
      <div className={styles.field}>
        <label htmlFor="d-motivo">Motivo <small>(obrigatório)</small></label>
        <input id="d-motivo" className={styles.input} maxLength={300} placeholder="Ex.: aprovado por engano" value={motivo} onChange={e => { setMotivo(e.target.value); setErro(''); }} />
      </div>
    </Modal>
  );
}

// ---- Exportar CSV

export function ModalExportar({ temporadaId, ano, filtro, quantidade, rotuloFiltro, onFechar }: {
  temporadaId: string; ano: number; filtro: Omit<FiltroDeExportacao, 'cpf' | 'contato' | 'ficha'>; quantidade: number; rotuloFiltro: string; onFechar: (exportou: boolean) => void;
}) {
  const [cpf, setCpf] = useState(false);
  const [contato, setContato] = useState(false);
  const [ficha, setFicha] = useState(true);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function baixar() {
    setSalvando(true);
    setErro('');
    try {
      await exportarCsv(temporadaId, { ...filtro, cpf, contato, ficha }, `inscricoes-${ano}.csv`);
      onFechar(true);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível exportar.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Exportar lista (CSV)" erro={erro} salvando={salvando} rotuloConfirmar="Baixar CSV" onConfirmar={baixar} onFechar={() => onFechar(false)}>
      <p className={styles.modalTexto}>Exporta as fichas do filtro atual ({rotuloFiltro}: {quantidade} {quantidade === 1 ? 'ficha' : 'fichas'}). O arquivo abre direto no Excel.</p>
      <div className={styles.field}>
        <label>Colunas</label>
        <label className={styles.check}><input type="checkbox" checked disabled /> Nome, idade, perfil, categorias, situação e financeiro</label>
        <label className={styles.check}><input type="checkbox" checked={cpf} onChange={e => setCpf(e.target.checked)} /> CPF</label>
        <label className={styles.check}><input type="checkbox" checked={contato} onChange={e => setContato(e.target.checked)} /> E-mail e telefone</label>
        <label className={styles.check}><input type="checkbox" checked={ficha} onChange={e => setFicha(e.target.checked)} /> Altura, peso e posição</label>
      </div>
      <div className={styles.aviso}>CPF, e-mail e telefone são dados pessoais (LGPD): marque só se precisar e guarde o arquivo com cuidado. Cada exportação fica registrada (quem, quando e quais colunas).</div>
    </Modal>
  );
}

// ---- Importar planilha

const ROTULO_RESULTADO = { Criar: 'será criada', Ignorar: 'ignorada', Erro: 'com erro' } as const;

export function ModalImportar({ temporadaId, onConcluido, onFechar }: { temporadaId: string; onConcluido: (r: ResultadoDaImportacao) => void; onFechar: () => void }) {
  const [csv, setCsv] = useState('');
  const [arquivo, setArquivo] = useState('');
  const [previa, setPrevia] = useState<PreviaDaImportacao | null>(null);
  const [consentimento, setConsentimento] = useState(false);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function escolher(f: File | undefined) {
    setPrevia(null);
    setErro('');
    if (!f) return;
    setArquivo(f.name);
    setSalvando(true);
    try {
      const texto = await f.text();
      setCsv(texto);
      setPrevia(await previaDaImportacao(temporadaId, texto));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível ler a planilha.');
    } finally {
      setSalvando(false);
    }
  }

  async function importar() {
    if (!previa || previa.aContar === 0) return setErro('Não há nenhuma linha para importar.');
    if (!consentimento) return setErro('Confirme o consentimento LGPD para importar.');
    setSalvando(true);
    setErro('');
    try {
      onConcluido(await importarPlanilha(temporadaId, csv));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível importar.');
      setSalvando(false);
    }
  }

  return (
    <Modal titulo="Importar planilha de inscrições" largo erro={erro} salvando={salvando}
      rotuloConfirmar={previa ? `Importar ${previa.aContar} ${previa.aContar === 1 ? 'ficha' : 'fichas'}` : 'Importar'} onConfirmar={importar} onFechar={onFechar}>
      <div className={styles.aviso} style={{ background: 'var(--badge-bg)', color: 'var(--text-primary)' }}>
        Cria fichas a partir de um CSV. <b>Nada é gravado antes da conferência</b>: primeiro você vê o que seria criado, ignorado ou com erro.
        Colunas: nome, cpf, nascimento, sexo, e-mail, telefone, categoria (várias separadas por |), altura, peso, posição.
      </div>
      <div className={styles.field}>
        <label htmlFor="imp-arq">Arquivo CSV</label>
        <input id="imp-arq" type="file" accept=".csv,text/csv,text/plain" onChange={e => void escolher(e.target.files?.[0])} />
        {arquivo && <span className={styles.mutado}>{arquivo}</span>}
      </div>
      {previa && (
        <>
          <p className={styles.modalTexto}>
            {previa.total} linhas: <b>{previa.aContar}</b> serão criadas · <b>{previa.ignoradas}</b> ignoradas · <b>{previa.comErro}</b> com erro (as linhas com erro não entram).
          </p>
          <div className={styles.tabelaWrap} style={{ maxHeight: 260, overflow: 'auto' }}>
            <table className={styles.tabela}>
              <thead><tr><th>Linha</th><th>Pessoa</th><th>Resultado</th></tr></thead>
              <tbody>
                {previa.linhas.map(l => (
                  <tr key={l.linha}>
                    <td>{l.linha}</td>
                    <td>{l.nome || '—'}</td>
                    <td>
                      <span className={`${styles.selo} ${l.resultado === 'Criar' ? styles.stEfetivada : l.resultado === 'Erro' ? styles.stRecusada : styles.stPendente}`}>{ROTULO_RESULTADO[l.resultado]}</span>
                      {[...l.mensagens, ...l.avisos].map((m, i) => <div key={i} className={styles.mini}>{m}</div>)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <label className={styles.check} style={{ marginTop: 12 }}>
            <input type="checkbox" checked={consentimento} onChange={e => { setConsentimento(e.target.checked); setErro(''); }} />
            Declaro que a associação tem o consentimento LGPD das pessoas desta planilha.
          </label>
        </>
      )}
    </Modal>
  );
}

import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type CampeonatoDetalhe,
  type StatusTemporada,
  type Temporada,
  criarTemporada,
  editarCampeonato,
  encerrarInscricoes,
  encerrarTemporada,
  iniciarTemporada,
  obterCampeonato,
} from '../../api/campeonatos';
import DataInput, { dataValida as dataValidaGeral, ddmmParaIso } from '../../components/DataInput/DataInput';
import { formatarData } from '../../utils/formatacao';
import styles from './DetalheCampeonato.module.css';

const STATUS_LABEL: Record<StatusTemporada, string> = {
  InscricoesAbertas: 'Inscrições abertas',
  InscricoesEncerradas: 'Inscrições encerradas',
  EmAndamento: 'Em andamento',
  Encerrada: 'Encerrada',
};

const STATUS_PILL: Record<StatusTemporada, string> = {
  InscricoesAbertas: styles.pillAberta,
  InscricoesEncerradas: styles.pillEncerradaInsc,
  EmAndamento: styles.pillEmAndamento,
  Encerrada: styles.pillEncerrada,
};

const dataValida = (v: string) => dataValidaGeral(v, 2000, 2100);

export default function DetalheCampeonato() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [camp, setCamp] = useState<CampeonatoDetalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [modalAberto, setModalAberto] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [editandoNome, setEditandoNome] = useState<string | null>(null);
  const [erroNome, setErroNome] = useState('');

  const anoAtual = new Date().getFullYear();
  const [form, setForm] = useState({ ano: String(anoAtual), dataInicio: '', dataFim: '' });
  const [formErros, setFormErros] = useState({ ano: '', dataInicio: '', dataFim: '' });
  const anoRef = useRef<HTMLInputElement>(null);

  async function carregar() {
    if (!id) return;
    try {
      setLoading(true);
      setErro('');
      setCamp(await obterCampeonato(id));
    } catch {
      setErro('Não foi possível carregar o campeonato.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { carregar(); }, [id]);

  useEffect(() => {
    if (modalAberto) {
      setForm({ ano: String(anoAtual), dataInicio: '', dataFim: '' });
      setFormErros({ ano: '', dataInicio: '', dataFim: '' });
      setTimeout(() => anoRef.current?.focus(), 50);
    }
  }, [modalAberto]);

  function validar() {
    const erros = { ano: '', dataInicio: '', dataFim: '' };
    const anoNum = Number(form.ano);
    if (!anoNum || anoNum < 2000 || anoNum > 2100) erros.ano = 'Ano inválido.';
    if (!dataValida(form.dataInicio)) erros.dataInicio = 'Data inválida (DD/MM/AAAA).';
    if (!dataValida(form.dataFim)) erros.dataFim = 'Data inválida (DD/MM/AAAA).';
    if (dataValida(form.dataInicio) && dataValida(form.dataFim)) {
      if (ddmmParaIso(form.dataFim) <= ddmmParaIso(form.dataInicio))
        erros.dataFim = 'Data fim deve ser posterior à data início.';
    }
    setFormErros(erros);
    return !erros.ano && !erros.dataInicio && !erros.dataFim;
  }

  async function salvar() {
    if (!camp || !validar()) return;
    setSalvando(true);
    try {
      await criarTemporada(camp.id, {
        ano: Number(form.ano),
        dataInicioInscricoes: ddmmParaIso(form.dataInicio),
        dataFimInscricoes: ddmmParaIso(form.dataFim),
      });
      setModalAberto(false);
      await carregar();
    } catch (e: unknown) {
      setFormErros(p => ({ ...p, ano: e instanceof Error ? e.message : 'Erro ao criar.' }));
    } finally {
      setSalvando(false);
    }
  }

  async function salvarNome() {
    if (!camp || editandoNome === null) return;
    const nome = editandoNome.trim();
    if (nome.length < 3) { setErroNome('Informe o nome (mín. 3 caracteres).'); return; }
    setSalvando(true);
    try {
      await editarCampeonato(camp.id, { nome });
      setEditandoNome(null);
      await carregar();
    } catch (e) {
      setErroNome(e instanceof Error ? e.message : 'Erro ao editar o campeonato.');
    } finally {
      setSalvando(false);
    }
  }

  async function acao(t: Temporada) {
    if (!camp) return;
    try {
      if (t.status === 'InscricoesAbertas') await encerrarInscricoes(camp.id, t.id);
      else if (t.status === 'InscricoesEncerradas') await iniciarTemporada(camp.id, t.id);
      else if (t.status === 'EmAndamento') await encerrarTemporada(camp.id, t.id);
      await carregar();
    } catch {
      // silencioso
    }
  }

  function labelAcao(status: StatusTemporada): string | null {
    if (status === 'InscricoesAbertas') return 'Encerrar inscrições';
    if (status === 'InscricoesEncerradas') return 'Iniciar temporada';
    if (status === 'EmAndamento') return 'Encerrar temporada';
    return null;
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !camp) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Campeonato não encontrado.'}</p></div>;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/associacoes/${camp.associacaoId}`)}>
          ‹ Voltar
        </button>
      </div>

      <div className={styles.card}>
        <h1 className={styles.nome}>{camp.nome}</h1>
        <button className={styles.btnSecondary} style={{ marginTop: 8 }} onClick={() => { setErroNome(''); setEditandoNome(camp.nome); }}>
          Editar nome
        </button>
      </div>

      <div className={styles.sectionHeader}>
        <span className={styles.sectionTitle}>
          Temporadas <span className={styles.badge}>{camp.temporadas.length}</span>
        </span>
        <button className={styles.btnPrimary} onClick={() => setModalAberto(true)}>
          + Nova temporada
        </button>
      </div>

      {camp.temporadas.length === 0 ? (
        <p className={styles.empty}>Nenhuma temporada cadastrada.</p>
      ) : (
        <div className={styles.list}>
          {camp.temporadas.map((t: Temporada) => {
            const btnLabel = labelAcao(t.status);
            return (
              <div key={t.id} className={styles.tempCard}>
                <div className={styles.tempInfo}>
                  <div className={styles.tempAno}>{t.ano}</div>
                  <div className={styles.tempDatas}>
                    Inscrições: {formatarData(t.dataInicioInscricoes)} – {formatarData(t.dataFimInscricoes)}
                  </div>
                </div>
                <div className={styles.tempActions}>
                  <span className={`${styles.pill} ${STATUS_PILL[t.status]}`}>
                    {STATUS_LABEL[t.status]}
                  </span>
                  {btnLabel && (
                    <button className={styles.btnAction} onClick={() => acao(t)}>
                      {btnLabel}
                    </button>
                  )}
                  <button
                    className={styles.btnDetalhe}
                    aria-label="Ver detalhe da temporada"
                    onClick={() => navigate(`/temporadas/${t.id}`)}
                  >
                    ›
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {editandoNome !== null && (
        <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setEditandoNome(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Editar campeonato</h2>
            <div className={styles.field}>
              <label htmlFor="camp-nome-edit">Nome</label>
              <input
                id="camp-nome-edit"
                className={styles.input}
                autoFocus
                maxLength={150}
                value={editandoNome}
                onChange={e => { setEditandoNome(e.target.value); setErroNome(''); }}
                onKeyDown={e => { if (e.key === 'Enter') salvarNome(); }}
              />
              {erroNome && <span className={styles.fieldErro}>{erroNome}</span>}
            </div>
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setEditandoNome(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvarNome} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
            </div>
          </div>
        </div>
      )}

      {modalAberto && (
        <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) setModalAberto(false); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Nova temporada</h2>

            <div className={styles.field}>
              <label htmlFor="temp-ano">Ano</label>
              <input
                id="temp-ano"
                ref={anoRef}
                type="number"
                className={styles.input}
                min={2000}
                max={2100}
                value={form.ano}
                onChange={e => { setForm(p => ({ ...p, ano: e.target.value })); setFormErros(p => ({ ...p, ano: '' })); }}
              />
              {formErros.ano && <span className={styles.fieldErro}>{formErros.ano}</span>}
            </div>

            <div className={styles.fieldsRow}>
              <div className={styles.field}>
                <label htmlFor="temp-inicio">Início das inscrições</label>
                <DataInput anoMin={2000} anoMax={2100} id="temp-inicio"
                  value={form.dataInicio}
                  onChange={v => { setForm(p => ({ ...p, dataInicio: v })); setFormErros(p => ({ ...p, dataInicio: '' })); }}
                />
                {formErros.dataInicio && <span className={styles.fieldErro}>{formErros.dataInicio}</span>}
              </div>
              <div className={styles.field}>
                <label htmlFor="temp-fim">Fim das inscrições</label>
                <DataInput anoMin={2000} anoMax={2100} id="temp-fim"
                  value={form.dataFim}
                  onChange={v => { setForm(p => ({ ...p, dataFim: v })); setFormErros(p => ({ ...p, dataFim: '' })); }}
                />
                {formErros.dataFim && <span className={styles.fieldErro}>{formErros.dataFim}</span>}
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModalAberto(false)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>
                {salvando ? 'Salvando...' : 'Criar temporada'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

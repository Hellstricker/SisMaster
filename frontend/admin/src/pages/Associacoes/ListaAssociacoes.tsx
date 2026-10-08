import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  type Associacao,
  alterarStatusAssociacao,
  criarAssociacao,
  listarAssociacoes,
} from '../../api/associacoes';
import styles from './ListaAssociacoes.module.css';

export default function ListaAssociacoes() {
  const navigate = useNavigate();
  const [associacoes, setAssociacoes] = useState<Associacao[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');

  const [busca, setBusca] = useState('');
  const [filtro, setFiltro] = useState('');

  const [modalAberto, setModalAberto] = useState(false);
  const [form, setForm] = useState({ nome: '', sigla: '', uf: '' });
  const [formErros, setFormErros] = useState({ nome: '', sigla: '', uf: '' });
  const [salvando, setSalvando] = useState(false);
  const nomeRef = useRef<HTMLInputElement>(null);

  async function carregar() {
    try {
      setLoading(true);
      setErro('');
      setAssociacoes(await listarAssociacoes());
    } catch {
      setErro('Não foi possível carregar as associações.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { carregar(); }, []);

  useEffect(() => {
    if (modalAberto) setTimeout(() => nomeRef.current?.focus(), 50);
  }, [modalAberto]);

  function fecharModal() {
    setModalAberto(false);
    setForm({ nome: '', sigla: '', uf: '' });
    setFormErros({ nome: '', sigla: '', uf: '' });
  }

  function validar() {
    const erros = { nome: '', sigla: '', uf: '' };
    if (form.nome.trim().length < 3) erros.nome = 'Informe o nome (mín. 3 caracteres).';
    if (form.sigla.trim().length < 1) erros.sigla = 'Informe a sigla.';
    if (!/^[A-Za-z]{2}$/.test(form.uf)) erros.uf = 'UF deve ter 2 letras.';
    setFormErros(erros);
    return !erros.nome && !erros.sigla && !erros.uf;
  }

  async function salvar() {
    if (!validar()) return;
    setSalvando(true);
    try {
      await criarAssociacao({ nome: form.nome.trim(), sigla: form.sigla.trim().toUpperCase(), uf: form.uf.trim().toUpperCase() });
      fecharModal();
      await carregar();
    } catch (e: unknown) {
      setFormErros(prev => ({ ...prev, nome: e instanceof Error ? e.message : 'Erro ao salvar.' }));
    } finally {
      setSalvando(false);
    }
  }

  async function toggleAtiva(a: Associacao) {
    try {
      await alterarStatusAssociacao(a.id, !a.ativa);
      setAssociacoes(prev => prev.map(x => x.id === a.id ? { ...x, ativa: !x.ativa } : x));
    } catch {
      // silencioso — não bloqueia a UI
    }
  }

  const visiveis = associacoes.filter(a => {
    const q = busca.toLowerCase();
    const match = a.nome.toLowerCase().includes(q) || a.sigla.toLowerCase().includes(q) || a.uf.toLowerCase().includes(q);
    const status = !filtro || (filtro === 'ativa' ? a.ativa : !a.ativa);
    return match && status;
  });

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <div className={styles.titulo}>
          Associações
          <span className={styles.badge}>{associacoes.length}</span>
        </div>
        <button className={styles.btnPrimary} onClick={() => setModalAberto(true)}>
          + Nova associação
        </button>
      </div>

      <div className={styles.searchRow}>
        <input
          className={styles.input}
          placeholder="Buscar por nome ou sigla..."
          value={busca}
          onChange={e => setBusca(e.target.value)}
        />
        <select className={styles.select} value={filtro} onChange={e => setFiltro(e.target.value)}>
          <option value="">Todas</option>
          <option value="ativa">Ativas</option>
          <option value="inativa">Inativas</option>
        </select>
      </div>

      {loading && <p className={styles.info}>Carregando...</p>}
      {erro && <p className={styles.erroGlobal}>{erro}</p>}

      {!loading && !erro && visiveis.length === 0 && (
        <p className={styles.empty}>Nenhuma associação encontrada.</p>
      )}

      <div className={styles.list}>
        {visiveis.map(a => (
          <div key={a.id} className={styles.card}>
            <div className={styles.cardInfo}>
              <div className={styles.cardNome}>{a.nome}</div>
              <div className={styles.cardMeta}>
                <span>{a.sigla}</span>
                <span>{a.uf}</span>
                <span>{a.totalCampeonatos} campeonato{a.totalCampeonatos !== 1 ? 's' : ''}</span>
              </div>
            </div>
            <div className={styles.cardAcoes}>
              <span className={`${styles.pill} ${a.ativa ? styles.pillAtiva : styles.pillInativa}`}>
                {a.ativa ? 'Ativa' : 'Inativa'}
              </span>
              <button className={styles.btnToggle} onClick={() => toggleAtiva(a)}>
                {a.ativa ? 'Desativar' : 'Ativar'}
              </button>
              <button
                className={styles.btnDetalhe}
                aria-label="Ver detalhe"
                onClick={() => navigate(`/associacoes/${a.id}`)}
              >
                ›
              </button>
            </div>
          </div>
        ))}
      </div>

      {modalAberto && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) fecharModal(); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Nova associação</h2>

            <div className={styles.field}>
              <label htmlFor="nome">Nome</label>
              <input
                id="nome"
                ref={nomeRef}
                className={styles.input}
                placeholder="Associação Basketball São Paulo"
                value={form.nome}
                onChange={e => { setForm(p => ({ ...p, nome: e.target.value })); setFormErros(p => ({ ...p, nome: '' })); }}
              />
              {formErros.nome && <span className={styles.fieldErro}>{formErros.nome}</span>}
            </div>

            <div className={styles.fieldsRow}>
              <div className={styles.field}>
                <label htmlFor="sigla">Sigla</label>
                <input
                  id="sigla"
                  className={styles.input}
                  maxLength={10}
                  placeholder="ABSP"
                  value={form.sigla}
                  onChange={e => { setForm(p => ({ ...p, sigla: e.target.value.toUpperCase() })); setFormErros(p => ({ ...p, sigla: '' })); }}
                />
                {formErros.sigla && <span className={styles.fieldErro}>{formErros.sigla}</span>}
              </div>
              <div className={styles.field}>
                <label htmlFor="uf">UF</label>
                <input
                  id="uf"
                  className={styles.input}
                  maxLength={2}
                  placeholder="SP"
                  value={form.uf}
                  onChange={e => { setForm(p => ({ ...p, uf: e.target.value.toUpperCase() })); setFormErros(p => ({ ...p, uf: '' })); }}
                />
                {formErros.uf && <span className={styles.fieldErro}>{formErros.uf}</span>}
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={fecharModal} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>
                {salvando ? 'Salvando...' : 'Criar associação'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type AssociacaoDetalhe,
  type Campeonato,
  alterarStatusAssociacao,
  criarCampeonato,
  editarAssociacao,
  obterAssociacao,
} from '../../api/associacoes';
import styles from './DetalheAssociacao.module.css';

type ModalTipo = 'editar' | 'campeonato' | null;

export default function DetalheAssociacao() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [assoc, setAssoc] = useState<AssociacaoDetalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');

  const [modal, setModal] = useState<ModalTipo>(null);
  const [salvando, setSalvando] = useState(false);

  const [formEditar, setFormEditar] = useState({ nome: '', sigla: '', uf: '' });
  const [errosEditar, setErrosEditar] = useState({ nome: '', sigla: '', uf: '' });

  const [formCamp, setFormCamp] = useState({ nome: '' });
  const [errosCamp, setErrosCamp] = useState({ nome: '' });

  const nomeEditarRef = useRef<HTMLInputElement>(null);
  const nomeCampRef = useRef<HTMLInputElement>(null);

  async function carregar() {
    if (!id) return;
    try {
      setLoading(true);
      setErro('');
      setAssoc(await obterAssociacao(id));
    } catch {
      setErro('Não foi possível carregar a associação.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { carregar(); }, [id]);

  useEffect(() => {
    if (modal === 'editar') {
      setFormEditar({ nome: assoc?.nome ?? '', sigla: assoc?.sigla ?? '', uf: assoc?.uf ?? '' });
      setErrosEditar({ nome: '', sigla: '', uf: '' });
      setTimeout(() => nomeEditarRef.current?.focus(), 50);
    }
    if (modal === 'campeonato') {
      setFormCamp({ nome: '' });
      setErrosCamp({ nome: '' });
      setTimeout(() => nomeCampRef.current?.focus(), 50);
    }
  }, [modal]);

  async function toggleAtiva() {
    if (!assoc) return;
    try {
      await alterarStatusAssociacao(assoc.id, !assoc.ativa);
      setAssoc(prev => prev ? { ...prev, ativa: !prev.ativa } : prev);
    } catch {
      // silencioso
    }
  }

  function validarEditar() {
    const erros = { nome: '', sigla: '', uf: '' };
    if (formEditar.nome.trim().length < 3) erros.nome = 'Informe o nome (mín. 3 caracteres).';
    if (formEditar.sigla.trim().length < 1) erros.sigla = 'Informe a sigla.';
    if (!/^[A-Za-z]{2}$/.test(formEditar.uf)) erros.uf = 'UF deve ter 2 letras.';
    setErrosEditar(erros);
    return !erros.nome && !erros.sigla && !erros.uf;
  }

  async function salvarEditar() {
    if (!assoc || !validarEditar()) return;
    setSalvando(true);
    try {
      await editarAssociacao(assoc.id, {
        nome: formEditar.nome.trim(),
        sigla: formEditar.sigla.trim().toUpperCase(),
        uf: formEditar.uf.trim().toUpperCase(),
      });
      setAssoc(prev => prev ? { ...prev, ...formEditar, sigla: formEditar.sigla.toUpperCase(), uf: formEditar.uf.toUpperCase() } : prev);
      setModal(null);
    } catch (e: unknown) {
      setErrosEditar(prev => ({ ...prev, nome: e instanceof Error ? e.message : 'Erro ao salvar.' }));
    } finally {
      setSalvando(false);
    }
  }

  function validarCamp() {
    const erros = { nome: '' };
    if (formCamp.nome.trim().length < 3) erros.nome = 'Informe o nome (mín. 3 caracteres).';
    setErrosCamp(erros);
    return !erros.nome;
  }

  async function salvarCampeonato() {
    if (!assoc || !validarCamp()) return;
    setSalvando(true);
    try {
      await criarCampeonato(assoc.id, { nome: formCamp.nome.trim() });
      setModal(null);
      await carregar();
    } catch (e: unknown) {
      setErrosCamp(prev => ({ ...prev, nome: e instanceof Error ? e.message : 'Erro ao criar.' }));
    } finally {
      setSalvando(false);
    }
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !assoc) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Associação não encontrada.'}</p></div>;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate('/associacoes')}>
          ‹ Associações
        </button>
      </div>

      <div className={styles.card}>
        <div className={styles.cardInfo}>
          <h1 className={styles.nome}>{assoc.nome}</h1>
          <div className={styles.meta}>
            <span><strong>{assoc.sigla}</strong></span>
            <span>{assoc.uf}</span>
          </div>
          <span className={`${styles.pill} ${assoc.ativa ? styles.pillAtiva : styles.pillInativa}`}>
            {assoc.ativa ? 'Ativa' : 'Inativa'}
          </span>
        </div>
        <div className={styles.cardActions}>
          <button className={styles.btnSecondary} onClick={() => navigate(`/associacoes/${assoc.id}/categorias`)}>
            Categorias
          </button>
          <button className={styles.btnSecondary} onClick={() => navigate(`/associacoes/${assoc.id}/locais`)}>
            Locais
          </button>
          <button className={styles.btnSecondary} onClick={() => setModal('editar')}>
            Editar
          </button>
          <button className={styles.btnToggle} onClick={toggleAtiva}>
            {assoc.ativa ? 'Desativar' : 'Ativar'}
          </button>
        </div>
      </div>

      <div className={styles.sectionHeader}>
        <span className={styles.sectionTitle}>
          Campeonatos
          <span className={styles.badge}>{assoc.campeonatos.length}</span>
        </span>
        <button className={styles.btnPrimary} onClick={() => setModal('campeonato')}>
          + Novo campeonato
        </button>
      </div>

      {assoc.campeonatos.length === 0 ? (
        <p className={styles.empty}>Nenhum campeonato cadastrado.</p>
      ) : (
        <div className={styles.list}>
          {assoc.campeonatos.map((c: Campeonato) => (
            <div key={c.id} className={styles.campCard}>
              <div className={styles.campNome}>{c.nome}</div>
              <button
                className={styles.btnDetalhe}
                aria-label="Ver detalhe do campeonato"
                onClick={() => navigate(`/campeonatos/${c.id}`)}
              >
                ›
              </button>
            </div>
          ))}
        </div>
      )}

      {modal === 'editar' && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModal(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Editar associação</h2>

            <div className={styles.field}>
              <label htmlFor="edit-nome">Nome</label>
              <input
                id="edit-nome"
                ref={nomeEditarRef}
                className={styles.input}
                value={formEditar.nome}
                onChange={e => { setFormEditar(p => ({ ...p, nome: e.target.value })); setErrosEditar(p => ({ ...p, nome: '' })); }}
              />
              {errosEditar.nome && <span className={styles.fieldErro}>{errosEditar.nome}</span>}
            </div>

            <div className={styles.fieldsRow}>
              <div className={styles.field}>
                <label htmlFor="edit-sigla">Sigla</label>
                <input
                  id="edit-sigla"
                  className={styles.input}
                  maxLength={10}
                  value={formEditar.sigla}
                  onChange={e => { setFormEditar(p => ({ ...p, sigla: e.target.value.toUpperCase() })); setErrosEditar(p => ({ ...p, sigla: '' })); }}
                />
                {errosEditar.sigla && <span className={styles.fieldErro}>{errosEditar.sigla}</span>}
              </div>
              <div className={styles.field}>
                <label htmlFor="edit-uf">UF</label>
                <input
                  id="edit-uf"
                  className={styles.input}
                  maxLength={2}
                  value={formEditar.uf}
                  onChange={e => { setFormEditar(p => ({ ...p, uf: e.target.value.toUpperCase() })); setErrosEditar(p => ({ ...p, uf: '' })); }}
                />
                {errosEditar.uf && <span className={styles.fieldErro}>{errosEditar.uf}</span>}
              </div>
            </div>

            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModal(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvarEditar} disabled={salvando}>
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {modal === 'campeonato' && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModal(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Novo campeonato</h2>

            <div className={styles.field}>
              <label htmlFor="camp-nome">Nome</label>
              <input
                id="camp-nome"
                ref={nomeCampRef}
                className={styles.input}
                placeholder="Campeonato Estadual Master"
                value={formCamp.nome}
                onChange={e => { setFormCamp(p => ({ ...p, nome: e.target.value })); setErrosCamp(p => ({ ...p, nome: '' })); }}
              />
              {errosCamp.nome && <span className={styles.fieldErro}>{errosCamp.nome}</span>}
            </div>


            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModal(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvarCampeonato} disabled={salvando}>
                {salvando ? 'Salvando...' : 'Criar campeonato'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

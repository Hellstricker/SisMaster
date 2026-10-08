import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { type Categoria, obterCategoriasDaAssociacao } from '../../api/categorias';
import { type TemporadaDetalhe, adicionarCategoria, obterTemporada, removerCategoria } from '../../api/temporadas';
import { formatarData, formatarMoeda } from '../../utils/formatacao';
import styles from './DetalheTemporada.module.css';

const STATUS_LABEL: Record<string, string> = {
  InscricoesAbertas: 'Inscrições abertas',
  InscricoesEncerradas: 'Inscrições encerradas',
  EmAndamento: 'Em andamento',
  Encerrada: 'Encerrada',
};

const STATUS_PILL: Record<string, string> = {
  InscricoesAbertas: styles.pillAberta,
  InscricoesEncerradas: styles.pillEncerradaInsc,
  EmAndamento: styles.pillEmAndamento,
  Encerrada: styles.pillEncerrada,
};

export default function DetalheTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [temporada, setTemporada] = useState<TemporadaDetalhe | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [modalAberto, setModalAberto] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [aRemover, setARemover] = useState<{ categoriaId: string; nome: string } | null>(null);
  const [removerErro, setRemoverErro] = useState('');

  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [form, setForm] = useState({ categoriaId: '' });
  const [formErro, setFormErro] = useState('');

  async function carregar() {
    if (!id) return;
    try {
      setLoading(true);
      setErro('');
      setTemporada(await obterTemporada(id));
    } catch {
      setErro('Não foi possível carregar a temporada.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { carregar(); }, [id]);

  useEffect(() => {
    if (!modalAberto || !temporada) return;
    setForm({ categoriaId: '' });
    setFormErro('');
    obterCategoriasDaAssociacao(temporada.associacaoId).then(setCategorias).catch(() => {});
  }, [modalAberto, temporada?.associacaoId]);

  async function confirmarRemocao() {
    if (!aRemover) return;
    setSalvando(true);
    try {
      await removerCategoria(id!, aRemover.categoriaId);
      setARemover(null);
      await carregar();
    } catch (e: unknown) {
      setRemoverErro(e instanceof Error ? e.message : 'Erro ao remover categoria.');
    } finally {
      setSalvando(false);
    }
  }

  async function salvar() {
    if (!form.categoriaId) { setFormErro('Selecione uma categoria.'); return; }
    setSalvando(true);
    try {
      await adicionarCategoria(id!, { categoriaId: form.categoriaId });
      setModalAberto(false);
      await carregar();
    } catch (e: unknown) {
      setFormErro(e instanceof Error ? e.message : 'Erro ao adicionar categoria.');
    } finally {
      setSalvando(false);
    }
  }

  const categoriasDisponiveis = categorias.filter(
    c => !temporada?.categorias.some(tc => tc.categoriaId === c.id)
  );

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !temporada) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/campeonatos/${temporada.campeonatoId}`)}>
          ‹ Voltar
        </button>
      </div>

      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div>
            <h1 className={styles.titulo}>Temporada {temporada.ano}</h1>
            <p className={styles.datas}>
              Inscrições: {formatarData(temporada.dataInicioInscricoes)} – {formatarData(temporada.dataFimInscricoes)}
            </p>
          </div>
          <span className={`${styles.pill} ${STATUS_PILL[temporada.status] ?? ''}`}>
            {STATUS_LABEL[temporada.status] ?? temporada.status}
          </span>
        </div>
      </div>

      <div className={styles.sectionHeader}>
        <span className={styles.sectionTitle}>
          Categorias <span className={styles.badge}>{temporada.categorias.length}</span>
        </span>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className={styles.btnPrimary} onClick={() => navigate(`/temporadas/${temporada.id}/inscricoes`)}>
            Inscrições
          </button>
          <button className={styles.btnPrimary} onClick={() => navigate(`/temporadas/${temporada.id}/equipes`)}>
            Equipes
          </button>
          <button className={styles.btnPrimary} onClick={() => navigate(`/temporadas/${temporada.id}/fases`)}>
            Fases
          </button>
          <button className={styles.btnPrimary} onClick={() => navigate(`/temporadas/${temporada.id}/jogos`)}>
            Jogos
          </button>
          <button className={styles.btnPrimary} onClick={() => setModalAberto(true)}>
            + Nova categoria
          </button>
        </div>
      </div>

      {temporada.categorias.length === 0 ? (
        <p className={styles.empty}>Nenhuma categoria cadastrada.</p>
      ) : (
        <div className={styles.list}>
          {temporada.categorias.map(tc => (
            <div key={tc.id} className={styles.catCard}>
              <div className={styles.catInfo}>
                <div className={styles.catNome}>{tc.nome}</div>
                <div className={styles.datas}>
                  {tc.idadeMinima > 0 ? `${tc.idadeMinima}+ anos` : 'Qualquer idade'}
                  {' · '}{tc.sexo ?? 'Mista'}
                  {' · '}{tc.valor == null ? 'Valor a definir' : formatarMoeda(tc.valor)}
                </div>
              </div>
              {temporada.status !== 'Encerrada' && (
                <button
                  className={styles.btnSecondary}
                  style={{ marginRight: 8 }}
                  title="Remover a categoria da temporada (só sem inscrições, equipes e fases)"
                  onClick={() => { setRemoverErro(''); setARemover({ categoriaId: tc.categoriaId, nome: tc.nome }); }}
                >
                  Remover
                </button>
              )}
              <button
                className={styles.btnDetalhe}
                aria-label="Ver inscrições da categoria" title="Ver inscrições da categoria"
                onClick={() => navigate(`/temporadas/${temporada.id}/inscricoes?categoria=${tc.id}`)}
              >
                ›
              </button>
            </div>
          ))}
        </div>
      )}

      {aRemover && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setARemover(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Remover categoria</h2>
            <p>Remover <b>{aRemover.nome}</b> desta temporada? Só é possível se não houver pedidos de inscrição (nem recusados), equipes ou fases nela.</p>
            {removerErro && <p className={styles.fieldErro}>{removerErro}</p>}
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setARemover(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={confirmarRemocao} disabled={salvando}>{salvando ? 'Removendo...' : 'Remover'}</button>
            </div>
          </div>
        </div>
      )}

      {modalAberto && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget) setModalAberto(false); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Nova categoria</h2>

            <div className={styles.field}>
              <label htmlFor="cat-select">Categoria</label>
              <select
                id="cat-select"
                className={styles.select}
                value={form.categoriaId}
                onChange={e => { setForm(p => ({ ...p, categoriaId: e.target.value })); setFormErro(''); }}
              >
                <option value="">Selecione...</option>
                {categoriasDisponiveis.map(c => (
                  <option key={c.id} value={c.id}>{c.nome}</option>
                ))}
              </select>
            </div>


            {formErro && <p className={styles.fieldErro}>{formErro}</p>}

            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setModalAberto(false)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>
                {salvando ? 'Salvando...' : 'Adicionar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

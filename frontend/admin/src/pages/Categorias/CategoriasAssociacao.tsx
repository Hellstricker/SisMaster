import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { obterAssociacao } from '../../api/associacoes';
import {
  type Categoria,
  type DadosCategoria,
  type Sexo,
  criarCategoria,
  editarCategoria,
  excluirCategoria,
  obterCategoriasDaAssociacao,
} from '../../api/categorias';
import styles from './Categorias.module.css';

const VAZIA: DadosCategoria = {
  nome: '',
  idadeMinima: 0,
  sexo: null,
  aceitaAbaixoIdadeMinima: false,
  minimoPeriodosEmQuadra: 1,
  minimoPeriodosForaQuadra: 1,
};

type Modal = { tipo: 'form'; categoria: Categoria | null } | { tipo: 'excluir'; categoria: Categoria };

/** Catálogo de categorias da associação: modelo padrão copiado (snapshot) ao vincular a uma temporada. */
export default function CategoriasAssociacao() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [nomeAssociacao, setNomeAssociacao] = useState('');
  const [categorias, setCategorias] = useState<Categoria[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [modal, setModal] = useState<Modal | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const [a, c] = await Promise.all([obterAssociacao(id), obterCategoriasDaAssociacao(id)]);
      setNomeAssociacao(a.nome);
      setCategorias(c);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar as categorias.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(null), 4000);
    return () => clearTimeout(t);
  }, [aviso]);

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !id) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Associação não encontrada.'}</p></div>;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/associacoes/${id}`)}>‹ {nomeAssociacao}</button>
      </div>

      <div className={styles.cabecalho}>
        <div>
          <h1 className={styles.titulo}>Categorias da associação</h1>
          <p className={styles.sub}>
            Catálogo de categorias da {nomeAssociacao}. Ao vincular uma categoria a uma temporada, estas regras são copiadas
            para a temporada (snapshot).
          </p>
        </div>
        <button className={styles.btnPrimary} onClick={() => setModal({ tipo: 'form', categoria: null })}>+ Nova categoria</button>
      </div>

      <div className={styles.aviso}>
        É o modelo padrão usado para pré-preencher uma categoria dentro de uma temporada. <b>Editar aqui não muda temporadas
        já criadas</b> — cada temporada guarda a sua cópia e pode ajustá-la na própria temporada.
      </div>

      <div className={styles.tabelaWrap}>
        <table className={styles.tabela}>
          <thead>
            <tr>
              <th>Nome</th><th>Idade mínima</th><th>Sexo</th><th>Abaixo da idade mínima</th>
              <th>Rodízio (em quadra / fora)</th><th>Uso</th><th className={styles.direita}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {categorias.length === 0 && <tr><td colSpan={7} className={styles.vazio}>Nenhuma categoria cadastrada.</td></tr>}
            {categorias.map(c => (
              <tr key={c.id}>
                <td className={styles.nome}>{c.nome}</td>
                <td>{c.idadeMinima > 0 ? `${c.idadeMinima} anos` : 'sem mínimo'}</td>
                <td><span className={`${styles.chipSexo} ${c.sexo ? styles.chipSexoDef : ''}`}>{c.sexo ?? 'Mista'}</span></td>
                <td>{c.aceitaAbaixoIdadeMinima ? 'Aceita' : <span className={styles.mutado}>Não</span>}</td>
                <td>{c.minimoPeriodosEmQuadra} / {c.minimoPeriodosForaQuadra}</td>
                <td className={styles.mutado}>{c.uso > 0 ? `Em ${c.uso} temporada${c.uso > 1 ? 's' : ''}` : 'Sem uso'}</td>
                <td>
                  <div className={styles.acoes}>
                    <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'form', categoria: c })}>Editar</button>
                    <button
                      className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`}
                      disabled={c.uso > 0}
                      title={c.uso > 0 ? `Usada em ${c.uso} temporada(s): não pode ser excluída` : ''}
                      onClick={() => setModal({ tipo: 'excluir', categoria: c })}
                    >
                      Excluir
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {modal?.tipo === 'form' && (
        <FormCategoria
          categoria={modal.categoria}
          onFechar={() => setModal(null)}
          onSalvar={async dados => {
            if (modal.categoria) await editarCategoria(id, modal.categoria.id, dados);
            else await criarCategoria(id, dados);
            setModal(null);
            setAviso({
              texto: modal.categoria ? 'Categoria atualizada. Temporadas já criadas não foram alteradas.' : 'Categoria criada.',
              erro: false,
            });
            await carregar();
          }}
        />
      )}

      {modal?.tipo === 'excluir' && (
        <ConfirmarExclusao
          categoria={modal.categoria}
          onFechar={() => setModal(null)}
          onConfirmar={async () => {
            await excluirCategoria(id, modal.categoria.id);
            setModal(null);
            setAviso({ texto: 'Categoria excluída.', erro: false });
            await carregar();
          }}
        />
      )}

      {aviso && <div className={`${styles.toast} ${aviso.erro ? styles.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

function FormCategoria({ categoria, onSalvar, onFechar }: {
  categoria: Categoria | null;
  onSalvar: (dados: DadosCategoria) => Promise<void>;
  onFechar: () => void;
}) {
  const [form, setForm] = useState({
    nome: categoria?.nome ?? VAZIA.nome,
    idadeMinima: String(categoria?.idadeMinima ?? VAZIA.idadeMinima),
    sexo: (categoria?.sexo ?? '') as Sexo | '',
    aceitaAbaixo: categoria?.aceitaAbaixoIdadeMinima ?? VAZIA.aceitaAbaixoIdadeMinima,
    em: String(categoria?.minimoPeriodosEmQuadra ?? VAZIA.minimoPeriodosEmQuadra),
    fora: String(categoria?.minimoPeriodosForaQuadra ?? VAZIA.minimoPeriodosForaQuadra),
  });
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  function campo<K extends keyof typeof form>(k: K, v: (typeof form)[K]) {
    setForm(p => ({ ...p, [k]: v }));
    setErro('');
  }

  async function salvar() {
    const nome = form.nome.trim();
    const idade = Number(form.idadeMinima);
    const em = Number(form.em);
    const fora = Number(form.fora);
    if (!nome) { setErro('Informe o nome.'); return; }
    if (!Number.isInteger(idade) || idade < 0 || idade > 120) { setErro('Idade mínima inválida.'); return; }
    if (![em, fora].every(n => Number.isInteger(n) && n >= 0 && n <= 4)) { setErro('Os mínimos de rodízio devem estar entre 0 e 4.'); return; }
    if (em + fora > 4) { setErro('A soma dos mínimos de rodízio não pode passar de 4 períodos.'); return; }

    setSalvando(true);
    try {
      await onSalvar({
        nome,
        idadeMinima: idade,
        sexo: form.sexo === '' ? null : form.sexo,
        aceitaAbaixoIdadeMinima: form.aceitaAbaixo,
        minimoPeriodosEmQuadra: em,
        minimoPeriodosForaQuadra: fora,
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>{categoria ? 'Editar categoria' : 'Nova categoria'}</h2>
        <div className={styles.grid}>
          <div className={`${styles.field} ${styles.cheio}`}>
            <label htmlFor="c-nome">Nome</label>
            <input id="c-nome" className={styles.input} maxLength={100} placeholder="Ex.: Master Masculino" autoFocus
              value={form.nome} onChange={e => campo('nome', e.target.value)} />
          </div>
          <div className={styles.field}>
            <label htmlFor="c-idade">Idade mínima</label>
            <input id="c-idade" className={styles.input} inputMode="numeric" value={form.idadeMinima}
              onChange={e => campo('idadeMinima', e.target.value.replace(/\D/g, '').slice(0, 3))} />
          </div>
          <div className={styles.field}>
            <label className={styles.check}>
              <input type="checkbox" checked={form.aceitaAbaixo} onChange={e => campo('aceitaAbaixo', e.target.checked)} />
              Aceita abaixo da idade mínima
            </label>
          </div>
          <div className={`${styles.field} ${styles.cheio}`}>
            <label>Sexo</label>
            <div className={styles.radios}>
              {([['', 'Mista'], ['Masculino', 'Masculino'], ['Feminino', 'Feminino']] as const).map(([valor, rotulo]) => (
                <label key={rotulo}>
                  <input type="radio" name="c-sexo" checked={form.sexo === valor} onChange={() => campo('sexo', valor)} /> {rotulo}
                </label>
              ))}
            </div>
          </div>
          <div className={styles.field}>
            <label htmlFor="c-em">Mínimo de períodos em quadra</label>
            <input id="c-em" className={styles.input} inputMode="numeric" value={form.em}
              onChange={e => campo('em', e.target.value.replace(/\D/g, '').slice(0, 1))} />
          </div>
          <div className={styles.field}>
            <label htmlFor="c-fora">Mínimo de períodos fora da quadra</label>
            <input id="c-fora" className={styles.input} inputMode="numeric" value={form.fora}
              onChange={e => campo('fora', e.target.value.replace(/\D/g, '').slice(0, 1))} />
          </div>
        </div>
        <p className={styles.dica}>Rodízio: dentro dos 4 períodos normais. A soma dos dois mínimos não pode passar de 4.</p>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </div>
    </div>
  );
}

function ConfirmarExclusao({ categoria, onConfirmar, onFechar }: {
  categoria: Categoria;
  onConfirmar: () => Promise<void>;
  onFechar: () => void;
}) {
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function confirmar() {
    setSalvando(true);
    try {
      await onConfirmar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao excluir.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal} style={{ maxWidth: 420 }}>
        <h2 className={styles.modalTitulo}>Excluir categoria</h2>
        <p className={styles.modalTexto}>Excluir a categoria “{categoria.nome}” da associação? Esta ação não pode ser desfeita.</p>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={`${styles.btnPrimary} ${styles.btnPerigoSolido}`} onClick={confirmar} disabled={salvando}>
            {salvando ? 'Excluindo...' : 'Excluir'}
          </button>
        </div>
      </div>
    </div>
  );
}

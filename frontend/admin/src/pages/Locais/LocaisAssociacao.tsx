import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { obterAssociacao } from '../../api/associacoes';
import { type LocalItem, criarLocal, editarLocal, excluirLocal, listarLocais } from '../../api/jogos';
import styles from '../Categorias/Categorias.module.css';

type Modal = { tipo: 'form'; local: LocalItem | null } | { tipo: 'excluir'; local: LocalItem };

const descricao = (l: LocalItem) => `${l.nome} — ${l.cidade}${l.estado ? `/${l.estado}` : ''}`;

/** Ginásios e quadras da associação: usados no agendamento dos jogos (Tela 8/9/10). */
export default function LocaisAssociacao() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [nomeAssociacao, setNomeAssociacao] = useState('');
  const [locais, setLocais] = useState<LocalItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [modal, setModal] = useState<Modal | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const [a, l] = await Promise.all([obterAssociacao(id), listarLocais(id)]);
      setNomeAssociacao(a.nome);
      setLocais(l);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os locais.');
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
          <h1 className={styles.titulo}>Locais de jogo</h1>
          <p className={styles.sub}>Ginásios e quadras da {nomeAssociacao}, usados ao agendar os jogos (inclusive rodadas em outras cidades).</p>
        </div>
        <button className={styles.btnPrimary} onClick={() => setModal({ tipo: 'form', local: null })}>+ Novo local</button>
      </div>

      <div className={styles.tabelaWrap}>
        <table className={styles.tabela}>
          <thead>
            <tr><th>Nome</th><th>Cidade</th><th>UF</th><th>Uso</th><th className={styles.direita}>Ações</th></tr>
          </thead>
          <tbody>
            {locais.length === 0 && <tr><td colSpan={5} className={styles.vazio}>Nenhum local cadastrado.</td></tr>}
            {locais.map(l => (
              <tr key={l.id}>
                <td className={styles.nome}>{l.nome}</td>
                <td>{l.cidade}</td>
                <td>{l.estado ?? <span className={styles.mutado}>—</span>}</td>
                <td className={styles.mutado}>{l.jogos ? `Em ${l.jogos} jogo${l.jogos > 1 ? 's' : ''}` : 'Sem uso'}</td>
                <td>
                  <div className={styles.acoes}>
                    <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'form', local: l })}>Editar</button>
                    <button
                      className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`}
                      disabled={!!l.jogos}
                      title={l.jogos ? `Usado em ${l.jogos} jogo(s): não pode ser excluído` : ''}
                      onClick={() => setModal({ tipo: 'excluir', local: l })}
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
        <FormLocal
          local={modal.local}
          onFechar={() => setModal(null)}
          onSalvar={async dados => {
            if (modal.local) await editarLocal(modal.local.id, dados);
            else await criarLocal(id, dados);
            setModal(null);
            setAviso(modal.local ? 'Local atualizado.' : 'Local criado.');
            await carregar();
          }}
        />
      )}

      {modal?.tipo === 'excluir' && (
        <ConfirmarExclusao
          local={modal.local}
          onFechar={() => setModal(null)}
          onConfirmar={async () => {
            await excluirLocal(modal.local.id);
            setModal(null);
            setAviso('Local excluído.');
            await carregar();
          }}
        />
      )}

      {aviso && <div className={styles.toast} role="status">{aviso}</div>}
    </div>
  );
}

function FormLocal({ local, onSalvar, onFechar }: {
  local: LocalItem | null;
  onSalvar: (dados: { nome: string; cidade: string; estado: string | null }) => Promise<void>;
  onFechar: () => void;
}) {
  const [form, setForm] = useState({ nome: local?.nome ?? '', cidade: local?.cidade ?? '', estado: local?.estado ?? '' });
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  const campo = (k: keyof typeof form, v: string) => { setForm(p => ({ ...p, [k]: v })); setErro(''); };

  async function salvar() {
    const nome = form.nome.trim();
    const cidade = form.cidade.trim();
    const estado = form.estado.trim();
    if (!nome) { setErro('Informe o nome do local.'); return; }
    if (!cidade) { setErro('Informe a cidade.'); return; }
    if (estado && estado.length !== 2) { setErro('A UF deve ter 2 letras.'); return; }

    setSalvando(true);
    try {
      await onSalvar({ nome, cidade, estado: estado ? estado.toUpperCase() : null });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>{local ? 'Editar local' : 'Novo local'}</h2>
        <div className={styles.grid}>
          <div className={`${styles.field} ${styles.cheio}`}>
            <label htmlFor="l-nome">Nome</label>
            <input id="l-nome" className={styles.input} maxLength={100} placeholder="Ex.: Ginásio Municipal" autoFocus
              value={form.nome} onChange={e => campo('nome', e.target.value)} />
          </div>
          <div className={styles.field}>
            <label htmlFor="l-cidade">Cidade</label>
            <input id="l-cidade" className={styles.input} maxLength={100} value={form.cidade} onChange={e => campo('cidade', e.target.value)} />
          </div>
          <div className={styles.field}>
            <label htmlFor="l-uf">UF</label>
            <input id="l-uf" className={styles.input} maxLength={2} placeholder="BA" value={form.estado}
              onChange={e => campo('estado', e.target.value.replace(/[^a-zA-Z]/g, '').toUpperCase())} />
          </div>
        </div>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
        </div>
      </div>
    </div>
  );
}

function ConfirmarExclusao({ local, onConfirmar, onFechar }: { local: LocalItem; onConfirmar: () => Promise<void>; onFechar: () => void }) {
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
        <h2 className={styles.modalTitulo}>Excluir local</h2>
        <p className={styles.modalTexto}>Excluir “{descricao(local)}”? Esta ação não pode ser desfeita.</p>
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

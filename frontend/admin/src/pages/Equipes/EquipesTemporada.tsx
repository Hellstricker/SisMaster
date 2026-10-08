import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  type AtletaItem,
  type EquipeItem,
  type EquipesDaTemporada,
  adicionarAtleta,
  criarEquipe,
  editarEquipe,
  excluirEquipe,
  obterEquipesDaTemporada,
  removerAtleta,
} from '../../api/equipes';
import { formatarMoeda } from '../../utils/formatacao';
import styles from './Equipes.module.css';

type Modal = { tipo: 'form'; equipe: EquipeItem | null } | { tipo: 'excluir'; equipe: EquipeItem };

/**
 * Equipes da temporada: por categoria, atletas efetivados (sem equipe) entram em equipes da MESMA categoria.
 * Só disponível depois de encerradas as inscrições.
 */
export default function EquipesTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [dados, setDados] = useState<EquipesDaTemporada | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [catId, setCatId] = useState<string | null>(null);
  const [modal, setModal] = useState<Modal | null>(null);
  const [destino, setDestino] = useState<Record<string, string>>({});
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const d = await obterEquipesDaTemporada(id);
      setDados(d);
      setCatId(atual => (atual && d.categorias.some(c => c.temporadaCategoriaId === atual) ? atual : d.categorias[0]?.temporadaCategoriaId ?? null));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar as equipes.');
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

  async function acao(fn: () => Promise<void>, sucesso: string) {
    try {
      await fn();
      setAviso({ texto: sucesso, erro: false });
      await carregar();
    } catch (e) {
      setAviso({ texto: e instanceof Error ? e.message : 'Operação não concluída.', erro: true });
    }
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !dados || !id) return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;

  const liberado = dados.permiteFormarEquipes;
  const cat = dados.categorias.find(c => c.temporadaCategoriaId === catId) ?? null;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/temporadas/${id}`)}>‹ Voltar</button>
      </div>

      <h1 className={styles.titulo}>Equipes — Temporada {dados.ano}</h1>
      <p className={styles.sub}>
        As equipes são criadas por categoria e recebem os atletas <b>efetivados</b> (aprovados e com a taxa paga) daquela categoria.
        O elenco é o conjunto desses vínculos.
      </p>

      {!liberado && (
        <div className={styles.banner}>
          <b>Formação de equipes indisponível.</b>{' '}
          {dados.status === 'Encerrada' ? 'A temporada está encerrada.' : 'Disponível depois de encerrar as inscrições da temporada.'}
        </div>
      )}

      <div className={styles.toolbar}>
        <div className={styles.tabs}>
          {dados.categorias.map(c => (
            <button key={c.temporadaCategoriaId} className={`${styles.tab} ${c.temporadaCategoriaId === catId ? styles.tabAtiva : ''}`}
              onClick={() => setCatId(c.temporadaCategoriaId)}>
              {c.nome}
              <small>{c.equipes.reduce((n, e) => n + e.atletas.length, 0) + c.semEquipe.length} atletas · {c.equipes.length} equipes</small>
            </button>
          ))}
        </div>
        <button className={styles.btnPrimary} disabled={!liberado || !cat} onClick={() => setModal({ tipo: 'form', equipe: null })}>+ Nova equipe</button>
      </div>

      {dados.categorias.length === 0 && <p className={styles.vazio}>A temporada não tem categorias.</p>}

      {cat && (
        <div className={styles.cols}>
          <div className={styles.card}>
            <h3 className={styles.cardTitulo}>Sem equipe <span className={styles.badge}>{cat.semEquipe.length}</span></h3>
            <p className={styles.dica}>Atletas efetivados desta categoria ainda sem equipe.</p>
            {cat.semEquipe.length === 0 && <p className={styles.vazio}>Todos os atletas efetivados desta categoria já têm equipe.</p>}
            {cat.semEquipe.map(a => (
              <LinhaDisponivel key={a.inscricaoCategoriaId} atleta={a} equipes={cat.equipes} liberado={liberado}
                escolhida={destino[a.inscricaoCategoriaId] ?? cat.equipes[0]?.id ?? ''}
                onEscolher={v => setDestino(p => ({ ...p, [a.inscricaoCategoriaId]: v }))}
                onAdicionar={equipeId => acao(() => adicionarAtleta(equipeId, a.inscricaoCategoriaId), `${a.nome} entrou na equipe.`)} />
            ))}
          </div>

          <div>
            {cat.equipes.length === 0 && <div className={`${styles.card} ${styles.vazio}`}>Nenhuma equipe nesta categoria. Crie a primeira em “+ Nova equipe”.</div>}
            {cat.equipes.map(e => (
              <div key={e.id} className={styles.equipe}>
                <div className={styles.equipeTopo}>
                  <span className={styles.cor} style={{ background: e.cor ?? '#6f6659' }} />
                  <span className={styles.equipeNome}>{e.nome}</span>
                  <span className={styles.cnt}>{e.atletas.length} atleta{e.atletas.length === 1 ? '' : 's'}</span>
                  <span className={styles.equipeAcoes}>
                    <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={!liberado} onClick={() => setModal({ tipo: 'form', equipe: e })}>Editar</button>
                    <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} disabled={!liberado} onClick={() => setModal({ tipo: 'excluir', equipe: e })}>Excluir</button>
                  </span>
                </div>
                {e.atletas.length === 0 ? <div className={styles.vazio}>Equipe sem atletas.</div> : (
                  <ul className={styles.elenco}>
                    {e.atletas.map(a => (
                      <li key={a.id}>
                        <span className={styles.nome}>{a.nome}</span>
                        {a.saldoEmAberto != null && <span className={styles.selo} title={`Saldo em aberto: ${formatarMoeda(a.saldoEmAberto)}`}>saldo em aberto</span>}
                        <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={!liberado}
                          onClick={() => acao(() => removerAtleta(e.id, a.id), `${a.nome} voltou para “Sem equipe”.`)}>Remover</button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {modal?.tipo === 'form' && cat && (
        <FormEquipe
          equipe={modal.equipe}
          nomeCategoria={cat.nome}
          onFechar={() => setModal(null)}
          onSalvar={async ({ nome, cor }) => {
            if (modal.equipe) await editarEquipe(modal.equipe.id, { nome, cor });
            else await criarEquipe(id, { temporadaCategoriaId: cat.temporadaCategoriaId, nome, cor });
            setModal(null);
            setAviso({ texto: modal.equipe ? 'Equipe atualizada.' : 'Equipe criada.', erro: false });
            await carregar();
          }}
        />
      )}

      {modal?.tipo === 'excluir' && (
        <ConfirmarExclusao
          equipe={modal.equipe}
          onFechar={() => setModal(null)}
          onConfirmar={async () => {
            await excluirEquipe(modal.equipe.id);
            setModal(null);
            setAviso({ texto: 'Equipe excluída.', erro: false });
            await carregar();
          }}
        />
      )}

      {aviso && <div className={`${styles.toast} ${aviso.erro ? styles.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

function LinhaDisponivel({ atleta, equipes, liberado, escolhida, onEscolher, onAdicionar }: {
  atleta: AtletaItem;
  equipes: EquipeItem[];
  liberado: boolean;
  escolhida: string;
  onEscolher: (equipeId: string) => void;
  onAdicionar: (equipeId: string) => void;
}) {
  const semOpcao = !liberado || equipes.length === 0;
  return (
    <div className={styles.disp}>
      <div className={styles.dispInfo}>
        <b>{atleta.nome}</b>
        <span>
          {atleta.perfil}
          {atleta.saldoEmAberto != null && <> · <span className={styles.selo}>saldo {formatarMoeda(atleta.saldoEmAberto)}</span></>}
        </span>
      </div>
      <select className={styles.select} value={escolhida} disabled={semOpcao} onChange={e => onEscolher(e.target.value)} aria-label={`Equipe de ${atleta.nome}`}>
        {equipes.map(e => <option key={e.id} value={e.id}>{e.nome}</option>)}
      </select>
      <button className={`${styles.btnPrimary} ${styles.btnSm}`} disabled={semOpcao || !escolhida} onClick={() => onAdicionar(escolhida)}>Adicionar</button>
    </div>
  );
}

function FormEquipe({ equipe, nomeCategoria, onSalvar, onFechar }: {
  equipe: EquipeItem | null;
  nomeCategoria: string;
  onSalvar: (d: { nome: string; cor: string }) => Promise<void>;
  onFechar: () => void;
}) {
  const [nome, setNome] = useState(equipe?.nome ?? '');
  const [cor, setCor] = useState(equipe?.cor ?? '#d9622b');
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);

  async function salvar() {
    if (!nome.trim()) { setErro('Informe o nome.'); return; }
    setSalvando(true);
    try {
      await onSalvar({ nome: nome.trim(), cor });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Erro ao salvar.');
      setSalvando(false);
    }
  }

  return (
    <div className={styles.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>{equipe ? 'Editar equipe' : 'Nova equipe'}</h2>
        <div className={styles.field}>
          <label htmlFor="eq-nome">Nome</label>
          <input id="eq-nome" className={styles.input} maxLength={100} placeholder="Ex.: Panteras" autoFocus value={nome}
            onChange={e => { setNome(e.target.value); setErro(''); }} />
        </div>
        <div className={styles.linha2}>
          <div className={styles.field}>
            <label htmlFor="eq-cor">Cor</label>
            <input id="eq-cor" type="color" className={`${styles.input} ${styles.inputCor}`} value={cor} onChange={e => setCor(e.target.value)} />
          </div>
          <div className={styles.field} style={{ flex: 1 }}>
            <label htmlFor="eq-cat">Categoria</label>
            <input id="eq-cat" className={styles.input} value={nomeCategoria} disabled />
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

function ConfirmarExclusao({ equipe, onConfirmar, onFechar }: {
  equipe: EquipeItem;
  onConfirmar: () => Promise<void>;
  onFechar: () => void;
}) {
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const n = equipe.atletas.length;

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
      <div className={styles.modal}>
        <h2 className={styles.modalTitulo}>Excluir equipe</h2>
        <p className={styles.modalTexto}>
          Excluir a equipe “{equipe.nome}”?{n > 0 ? ` O elenco (${n} atleta${n === 1 ? '' : 's'}) voltará para “Sem equipe”.` : ''}
        </p>
        {erro && <p className={styles.fieldErro}>{erro}</p>}
        <div className={styles.modalFooter}>
          <button className={styles.btnSecondary} onClick={onFechar} disabled={salvando}>Cancelar</button>
          <button className={`${styles.btnPrimary} ${styles.btnPerigoSolido}`} onClick={confirmar} disabled={salvando}>{salvando ? 'Excluindo...' : 'Excluir'}</button>
        </div>
      </div>
    </div>
  );
}

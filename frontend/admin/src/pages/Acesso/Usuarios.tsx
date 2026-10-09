import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { type Perfil, ROTULO_PERFIL } from '../../auth/sessao';
import { type Associacao, listarAssociacoes } from '../../api/associacoes';
import {
  type AcaoUsuario, type UsuarioResumo,
  acaoNoUsuario, criarUsuario, editarUsuario, listarUsuarios,
} from '../../api/usuarios';
import styles from '../Associacoes/ListaAssociacoes.module.css';
import local from './Usuarios.module.css';

const PERFIS: Perfil[] = ['Administrador', 'AdministradorDeAssociacao', 'Arbitro'];

interface Form {
  id?: string;
  nome: string;
  email: string;
  perfil: Perfil;
  associacoes: string[];
}

const TEXTO_CONFIRMA: Record<AcaoUsuario, (u: UsuarioResumo) => string> = {
  desativar: u => `Desativar ${u.nome}? A pessoa deixa de conseguir entrar; nada é apagado e dá para reativar depois.`,
  reativar: u => `Reativar ${u.nome}? A pessoa volta a poder entrar com a senha dela.`,
  desbloquear: u => `Liberar o acesso de ${u.nome}, bloqueado por tentativas de senha erradas?`,
  'redefinir-senha': u => `Gerar uma nova senha temporária para ${u.nome} e enviar por e-mail para ${u.email}? A senha atual deixa de valer.`,
};

export default function Usuarios() {
  const { usuario: eu } = useAuth();
  const global = eu?.perfil === 'Administrador';
  const perfisPermitidos = global ? PERFIS : (['Arbitro'] as Perfil[]);

  const [usuarios, setUsuarios] = useState<UsuarioResumo[]>([]);
  const [associacoes, setAssociacoes] = useState<Associacao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [filtroPerfil, setFiltroPerfil] = useState('');
  const [filtroSituacao, setFiltroSituacao] = useState('');
  const [busca, setBusca] = useState('');

  const [form, setForm] = useState<Form | null>(null);
  const [erroForm, setErroForm] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [confirma, setConfirma] = useState<{ usuario: UsuarioResumo; acao: AcaoUsuario } | null>(null);

  async function carregar() {
    try {
      setErro('');
      const [u, a] = await Promise.all([listarUsuarios(), listarAssociacoes()]);
      setUsuarios(u);
      setAssociacoes(a);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os usuários.');
    } finally {
      setCarregando(false);
    }
  }
  useEffect(() => { void carregar(); }, []);

  const nomeDaAssociacao = useMemo(() => new Map(associacoes.map(a => [a.id, a.sigla || a.nome])), [associacoes]);

  const visiveis = usuarios.filter(u => {
    const q = busca.trim().toLowerCase();
    return (!q || u.nome.toLowerCase().includes(q) || u.email.toLowerCase().includes(q))
      && (!filtroPerfil || u.perfil === filtroPerfil)
      && (!filtroSituacao || u.situacao === filtroSituacao);
  });

  const novo = () => {
    setErroForm('');
    setForm({
      nome: '', email: '', perfil: perfisPermitidos[perfisPermitidos.length - 1],
      associacoes: !global && eu?.associacoes.length === 1 ? [...eu.associacoes] : [],
    });
  };
  const editar = (u: UsuarioResumo) => {
    setErroForm('');
    setForm({ id: u.id, nome: u.nome, email: u.email, perfil: u.perfil ?? 'Arbitro', associacoes: [...u.associacoes] });
  };

  const alternarAssociacao = (id: string) =>
    setForm(f => f && ({ ...f, associacoes: f.associacoes.includes(id) ? f.associacoes.filter(x => x !== id) : [...f.associacoes, id] }));

  async function salvar() {
    if (!form) return;
    if (form.nome.trim().length < 3) return setErroForm('Informe o nome (mínimo 3 letras).');
    if (!form.id && !/^\S+@\S+\.\S+$/.test(form.email.trim())) return setErroForm('Informe um e-mail válido.');
    if (form.perfil !== 'Administrador' && form.associacoes.length === 0) return setErroForm('Marque ao menos uma associação.');
    setSalvando(true);
    setErroForm('');
    try {
      const dados = { nome: form.nome.trim(), perfil: form.perfil, associacoes: form.perfil === 'Administrador' ? [] : form.associacoes };
      if (form.id) {
        await editarUsuario(form.id, dados);
        setAviso('Alterações salvas.');
      } else {
        await criarUsuario({ ...dados, email: form.email.trim() });
        setAviso(`Usuário criado. A senha temporária foi enviada para ${form.email.trim()}; no primeiro acesso a pessoa define a própria senha.`);
      }
      setForm(null);
      await carregar();
    } catch (e) {
      setErroForm(e instanceof Error ? e.message : 'Não foi possível salvar.');
    } finally {
      setSalvando(false);
    }
  }

  async function confirmar() {
    if (!confirma) return;
    const { usuario, acao } = confirma;
    setSalvando(true);
    try {
      await acaoNoUsuario(usuario.id, acao);
      setAviso(acao === 'redefinir-senha' ? `Nova senha temporária enviada para ${usuario.email}.` : 'Feito.');
      setConfirma(null);
      await carregar();
    } catch (e) {
      setConfirma(null);
      setErro(e instanceof Error ? e.message : 'Não foi possível concluir a ação.');
    } finally {
      setSalvando(false);
    }
  }

  const vinculos = (u: UsuarioResumo) =>
    u.perfil === 'Administrador' ? 'Todas' : u.associacoes.map(a => nomeDaAssociacao.get(a) ?? '—').join(', ') || '—';

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <div className={styles.titulo}>
          Usuários e perfis
          <span className={styles.badge}>{usuarios.length}</span>
        </div>
        <button className={styles.btnPrimary} onClick={novo}>+ Novo usuário</button>
      </div>

      <div className={styles.searchRow}>
        <input className={styles.input} placeholder="Buscar por nome ou e-mail..." value={busca} onChange={e => setBusca(e.target.value)} />
        <select className={styles.select} value={filtroPerfil} onChange={e => setFiltroPerfil(e.target.value)} aria-label="Perfil">
          <option value="">Todos os perfis</option>
          {PERFIS.map(p => <option key={p} value={p}>{ROTULO_PERFIL[p]}</option>)}
        </select>
        <select className={styles.select} value={filtroSituacao} onChange={e => setFiltroSituacao(e.target.value)} aria-label="Situação">
          <option value="">Todas as situações</option>
          <option value="Ativo">Ativos</option>
          <option value="Desativado">Desativados</option>
          <option value="Bloqueado">Bloqueados</option>
        </select>
      </div>

      {aviso && <p className={styles.info} role="status">{aviso}</p>}
      {carregando && <p className={styles.info}>Carregando...</p>}
      {erro && <p className={styles.erroGlobal}>{erro}</p>}
      {!carregando && !erro && visiveis.length === 0 && <p className={styles.empty}>Nenhum usuário encontrado.</p>}

      <div className={styles.list}>
        {visiveis.map(u => {
          const souEu = u.id === eu?.id;
          return (
            <div key={u.id} className={`${styles.card} ${local.cartao}`}>
              <div className={local.topo}>
              <div className={styles.cardInfo}>
                <div className={styles.cardNome}>{u.nome}{souEu && ' (você)'}</div>
                <div className={`${styles.cardMeta} ${local.meta}`}>
                  <span>{u.email}</span>
                  <span>{u.perfil ? ROTULO_PERFIL[u.perfil] : '—'}</span>
                  <span>{vinculos(u)}</span>
                  {u.senhaTemporaria && <span>aguardando 1º acesso</span>}
                </div>
              </div>
              <span className={`${styles.pill} ${u.situacao === 'Ativo' ? styles.pillAtiva : styles.pillInativa}`}>{u.situacao}</span>
              </div>
              <div className={local.acoes}>
                <button className={styles.btnToggle} onClick={() => editar(u)}>Editar</button>
                {u.situacao === 'Bloqueado' && <button className={styles.btnToggle} onClick={() => setConfirma({ usuario: u, acao: 'desbloquear' })}>Desbloquear</button>}
                <button className={styles.btnToggle} onClick={() => setConfirma({ usuario: u, acao: 'redefinir-senha' })}>Redefinir senha</button>
                {u.situacao === 'Desativado'
                  ? <button className={styles.btnToggle} onClick={() => setConfirma({ usuario: u, acao: 'reativar' })}>Reativar</button>
                  : !souEu && <button className={styles.btnToggle} onClick={() => setConfirma({ usuario: u, acao: 'desativar' })}>Desativar</button>}
              </div>
            </div>
          );
        })}
      </div>

      {form && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget && !salvando) setForm(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>{form.id ? 'Editar usuário' : 'Novo usuário'}</h2>

            <div className={styles.field}>
              <label htmlFor="u-nome">Nome</label>
              <input id="u-nome" className={styles.input} value={form.nome} onChange={e => setForm({ ...form, nome: e.target.value })} />
            </div>
            <div className={styles.field}>
              <label htmlFor="u-email">E-mail (é o login)</label>
              <input id="u-email" className={styles.input} type="email" value={form.email} readOnly={!!form.id}
                onChange={e => setForm({ ...form, email: e.target.value })} />
            </div>
            <div className={styles.field}>
              <label htmlFor="u-perfil">Perfil</label>
              <select id="u-perfil" className={styles.select} value={form.perfil} disabled={form.id === eu?.id || perfisPermitidos.length === 1}
                onChange={e => setForm({ ...form, perfil: e.target.value as Perfil })}>
                {perfisPermitidos.map(p => <option key={p} value={p}>{ROTULO_PERFIL[p]}</option>)}
              </select>
            </div>
            {form.perfil !== 'Administrador' && (
              <div className={styles.field}>
                <label>Associações que pode acessar</label>
                {associacoes.filter(a => global || eu?.associacoes.includes(a.id)).map(a => (
                  <label key={a.id} style={{ display: 'flex', gap: 8, alignItems: 'center', fontWeight: 400 }}>
                    <input type="checkbox" style={{ width: 'auto', height: 'auto', flex: 'none' }}
                      checked={form.associacoes.includes(a.id)} onChange={() => alternarAssociacao(a.id)} />
                    {a.nome} ({a.sigla})
                  </label>
                ))}
              </div>
            )}
            {!form.id && (
              <p className={styles.info}>Ao salvar, o sistema gera uma senha temporária e a envia por e-mail para a pessoa. Ninguém, nem você, vê a senha. No primeiro acesso a pessoa define a própria senha.</p>
            )}
            {erroForm && <span className={styles.fieldErro}>{erroForm}</span>}

            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setForm(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar'}</button>
            </div>
          </div>
        </div>
      )}

      {confirma && (
        <div className={styles.overlay} onClick={e => { if (e.target === e.currentTarget && !salvando) setConfirma(null); }}>
          <div className={styles.modal}>
            <h2 className={styles.modalTitulo}>Confirmar</h2>
            <p>{TEXTO_CONFIRMA[confirma.acao](confirma.usuario)}</p>
            <div className={styles.modalFooter}>
              <button className={styles.btnSecondary} onClick={() => setConfirma(null)} disabled={salvando}>Cancelar</button>
              <button className={styles.btnPrimary} onClick={confirmar} disabled={salvando}>{salvando ? 'Aguarde...' : 'Confirmar'}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

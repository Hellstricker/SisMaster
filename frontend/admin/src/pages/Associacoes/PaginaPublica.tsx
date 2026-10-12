import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { type PaginaPublicaDados, obterAssociacao, obterPaginaPublica, salvarPaginaPublica } from '../../api/associacoes';
import base from '../Fases/Fases.module.css';

const VAZIA: PaginaPublicaDados = {
  missao: '', quemSomos: '', gestaoDaDiretoria: '', whatsapp: '', instagram: '', email: '', ondeJogamos: '', diretoria: [],
};

/**
 * Página pública da associação: o que aparece no site em "A associação" e "Contato". Cada associação tem o seu conteúdo
 * (texto simples). Só o que estiver preenchido aparece; cargo sem nome fica guardado, mas não vai para o site.
 */
export default function PaginaPublica() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [nome, setNome] = useState('');
  const [dados, setDados] = useState<PaginaPublicaDados>(VAZIA);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [aviso, setAviso] = useState('');

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const [a, p] = await Promise.all([obterAssociacao(id), obterPaginaPublica(id)]);
      setNome(a.nome);
      setDados({
        missao: p.missao ?? '', quemSomos: p.quemSomos ?? '', gestaoDaDiretoria: p.gestaoDaDiretoria ?? '', whatsapp: p.whatsapp ?? '',
        instagram: p.instagram ?? '', email: p.email ?? '', ondeJogamos: p.ondeJogamos ?? '',
        diretoria: p.diretoria.map(m => ({ cargo: m.cargo, nome: m.nome ?? '' })),
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar a página.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(''), 4000);
    return () => clearTimeout(t);
  }, [aviso]);

  const campo = (k: keyof Omit<PaginaPublicaDados, 'diretoria'>, v: string) => setDados(d => ({ ...d, [k]: v }));
  const membro = (i: number, k: 'cargo' | 'nome', v: string) =>
    setDados(d => ({ ...d, diretoria: d.diretoria.map((m, j) => (j === i ? { ...m, [k]: v } : m)) }));
  const mover = (i: number, delta: number) => setDados(d => {
    const lista = [...d.diretoria];
    const j = i + delta;
    if (j < 0 || j >= lista.length) return d;
    [lista[i], lista[j]] = [lista[j], lista[i]];
    return { ...d, diretoria: lista };
  });

  async function salvar() {
    if (!id) return;
    setSalvando(true);
    setErro('');
    try {
      await salvarPaginaPublica(id, dados);
      setAviso('Página salva. Já está no site.');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar.');
    } finally {
      setSalvando(false);
    }
  }

  if (loading) return <div className={base.page}><p className={base.info}>Carregando...</p></div>;

  return (
    <div className={base.page}>
      <p className={base.info} style={{ margin: '0 0 6px' }}>
        <button className={base.btnSecondary} onClick={() => navigate(`/associacoes/${id}`)}>‹ {nome || 'Associação'}</button>
      </p>
      <h1 className={base.titulo}>Página pública</h1>
      <p className={base.sub}>
        O que aparece no site em “A associação” e “Contato”. Só o que estiver preenchido aparece. Use texto simples (sem formatação).
      </p>

      {erro && <p className={base.erroGlobal} role="alert">{erro}</p>}

      <section className={base.card} style={{ marginBottom: 14 }}>
        <h2 className={base.modalTitulo}>A associação</h2>
        <div className={base.field}>
          <label htmlFor="pp-missao">Missão <small>(frase curta)</small></label>
          <input id="pp-missao" className={base.input} maxLength={160} value={dados.missao} onChange={e => campo('missao', e.target.value)} />
        </div>
        <div className={base.field}>
          <label htmlFor="pp-quem">Quem somos</label>
          <textarea id="pp-quem" className={base.input} rows={7} maxLength={2000} value={dados.quemSomos} onChange={e => campo('quemSomos', e.target.value)} />
          <small className={base.dica}>Uma linha em branco separa os parágrafos. Até 2.000 caracteres ({dados.quemSomos.length}).</small>
        </div>
      </section>

      <section className={base.card} style={{ marginBottom: 14 }}>
        <h2 className={base.modalTitulo}>Diretoria</h2>
        <div className={base.field} style={{ maxWidth: 220 }}>
          <label htmlFor="pp-gestao">Gestão</label>
          <input id="pp-gestao" className={base.input} maxLength={30} placeholder="2025/2026" value={dados.gestaoDaDiretoria} onChange={e => campo('gestaoDaDiretoria', e.target.value)} />
        </div>
        {dados.diretoria.map((m, i) => (
          <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1.6fr auto', gap: 8, alignItems: 'end', marginBottom: 8 }}>
            <div className={base.field} style={{ margin: 0 }}>
              <label htmlFor={`pp-cargo-${i}`}>Cargo</label>
              <input id={`pp-cargo-${i}`} className={base.input} maxLength={60} value={m.cargo} onChange={e => membro(i, 'cargo', e.target.value)} />
            </div>
            <div className={base.field} style={{ margin: 0 }}>
              <label htmlFor={`pp-nome-${i}`}>Nome</label>
              <input id={`pp-nome-${i}`} className={base.input} maxLength={100} value={m.nome} onChange={e => membro(i, 'nome', e.target.value)} />
            </div>
            <div style={{ display: 'flex', gap: 4 }}>
              <button className={`${base.btnSecondary} ${base.btnSm}`} aria-label={`Subir ${m.cargo}`} onClick={() => mover(i, -1)} disabled={i === 0}>↑</button>
              <button className={`${base.btnSecondary} ${base.btnSm}`} aria-label={`Descer ${m.cargo}`} onClick={() => mover(i, 1)} disabled={i === dados.diretoria.length - 1}>↓</button>
              <button className={`${base.btnSecondary} ${base.btnSm}`} aria-label={`Remover ${m.cargo}`} onClick={() => setDados(d => ({ ...d, diretoria: d.diretoria.filter((_, j) => j !== i) }))}>Remover</button>
            </div>
          </div>
        ))}
        <button className={`${base.btnSecondary} ${base.btnSm}`} disabled={dados.diretoria.length >= 30}
          onClick={() => setDados(d => ({ ...d, diretoria: [...d.diretoria, { cargo: '', nome: '' }] }))}>+ Adicionar cargo</button>
        <p className={base.dica} style={{ marginTop: 8 }}>Cargo sem nome não aparece no site.</p>
      </section>

      <section className={base.card} style={{ marginBottom: 14 }}>
        <h2 className={base.modalTitulo}>Contato</h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
          <div className={base.field}><label htmlFor="pp-whats">WhatsApp</label><input id="pp-whats" className={base.input} maxLength={30} placeholder="(71) 9 0000-0000" value={dados.whatsapp} onChange={e => campo('whatsapp', e.target.value)} /></div>
          <div className={base.field}><label htmlFor="pp-insta">Instagram</label><input id="pp-insta" className={base.input} maxLength={60} placeholder="@usuario" value={dados.instagram} onChange={e => campo('instagram', e.target.value)} /></div>
          <div className={base.field}><label htmlFor="pp-email">E-mail</label><input id="pp-email" type="email" className={base.input} maxLength={150} placeholder="contato@..." value={dados.email} onChange={e => campo('email', e.target.value)} /></div>
          <div className={base.field}><label htmlFor="pp-onde">Onde jogamos</label><input id="pp-onde" className={base.input} maxLength={150} placeholder="Ginásio, cidade/UF" value={dados.ondeJogamos} onChange={e => campo('ondeJogamos', e.target.value)} /></div>
        </div>
        <p className={base.dica}>Não coloque dados pessoais de atletas.</p>
      </section>

      <div style={{ display: 'flex', gap: 8 }}>
        <button className={base.btnPrimary} onClick={salvar} disabled={salvando}>{salvando ? 'Salvando...' : 'Salvar e publicar'}</button>
      </div>
      {aviso && <div className={base.toast} role="status">{aviso}</div>}
    </div>
  );
}

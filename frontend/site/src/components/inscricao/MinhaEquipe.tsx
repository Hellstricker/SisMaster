'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import CampoData from '@/components/CampoData';
import { brl } from '@/lib/detalhe';
import { type AtletaDaEquipe, type AtletaDoElenco, type DetalheDaEquipe, CLASSE_EQUIPE, MAXIMO_DE_ATLETAS, ROTULO_CONVITE, ROTULO_EQUIPE } from '@/lib/equipe';
import { cpfValido, dataCurta, dataParaIso, mascaraCpf, maiorDeIdade } from '@/lib/inscricao';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

const CLASSE_CONVITE = { Enviado: 'st-Pendente', Confirmado: 'st-Efetivada', Recusou: 'st-Recusada', Vencido: 'st-Recusada' } as const;

function Taxa({ a, taxa }: { a: AtletaDaEquipe; taxa: number | null }) {
  if (a.status !== 'Confirmado') return <span className="muted small">—</span>;
  if (a.taxaPaga) return <span className="badge st-Efetivada">{a.naEquipe ? 'Paga · no elenco' : 'Paga'}</span>;
  if (a.pedido === 'AguardandoPagamento') return <span className="badge st-AguardandoPagamento">A pagar{taxa != null ? ` ${brl(taxa)}` : ''}</span>;
  return <span className="muted small">Depois da aprovação</span>;
}

/** O acompanhamento da equipe pelo responsável (P08): quem confirmou, quem pagou a taxa, e o elenco enquanto o convite está aberto. */
export default function MinhaEquipe({ id }: { id: string }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [d, setD] = useState<DetalheDaEquipe | null>(null);
  const [erro, setErro] = useState('');
  const [naoAchou, setNaoAchou] = useState(false);
  const [aviso, setAviso] = useState('');
  const [aberto, setAberto] = useState(false);
  const [novo, setNovo] = useState<AtletaDoElenco>({ nome: '', cpf: '', nascimento: '', sexo: 'Masculino', email: '' });
  const [erroNovo, setErroNovo] = useState('');
  const [ocupado, setOcupado] = useState(false);

  const carregar = useCallback(async () => {
    const r = await chamarApi<DetalheDaEquipe>('GET', `/api/participante/equipes/${id}`);
    if (r.ok) setD(r.dados);
    else if (r.status === 404) setNaoAchou(true);
    else { setErro(r.erro); if (r.expirou) setSessao(null); }
  }, [id]);

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (s) void carregar();
  }, [carregar]);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Minha equipe</h1>
        <p className="muted">Entre na sua conta para ver a equipe.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent(`/minhas-inscricoes/equipes/${id}`)}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  if (naoAchou) return <div className="card conta centro"><h1>Equipe não encontrada</h1><p className="muted">Esta equipe não existe ou não é da sua conta.</p><Link className="btn block" href="/minhas-inscricoes">Minhas inscrições</Link></div>;
  if (erro) return <div className="card conta"><p className="aviso bad" role="alert">{erro}</p></div>;
  if (!d) return <div className="card conta centro"><p>Carregando…</p></div>;

  const abertoParaMudar = d.situacao === 'AguardandoConfirmacoes' || d.situacao === 'EmAnalise' || d.situacao === 'Aprovada';
  const efetivados = d.atletas.filter(a => a.taxaPaga).length;
  const ativos = d.atletas.filter(a => a.status === 'Enviado' || a.status === 'Confirmado' || a.status === 'Vencido');

  async function agir(acao: () => Promise<{ ok: boolean; erro?: string }>, ok: string) {
    setAviso(''); setErro('');
    const r = await acao();
    if (!r.ok) { setAviso(r.erro ?? 'Não foi possível concluir.'); return; }
    setAviso(ok);
    await carregar();
  }

  const reenviar = (a: AtletaDaEquipe) => agir(async () => {
    const r = await chamarApi('POST', `/api/participante/equipes/${id}/atletas/${a.id}/reenviar`);
    return r.ok ? { ok: true } : { ok: false, erro: r.erro };
  }, `Convite reenviado para ${a.nome}. O link anterior deixou de valer.`);

  const remover = (a: AtletaDaEquipe) => agir(async () => {
    const r = await chamarApi('DELETE', `/api/participante/equipes/${id}/atletas/${a.id}`);
    return r.ok ? { ok: true } : { ok: false, erro: r.erro };
  }, `${a.nome} saiu do elenco.`);

  async function adicionar() {
    const a = { ...novo, nome: novo.nome.trim(), email: novo.email.trim() };
    const iso = dataParaIso(a.nascimento);
    let m = '';
    if (a.nome.length < 3) m = 'Informe o nome completo.';
    else if (!cpfValido(a.cpf)) m = 'CPF inválido. Confira os 11 números.';
    else if (!iso) m = 'Informe a data de nascimento (DD/MM/AAAA).';
    else if (!a.email.includes('@')) m = 'Informe um e-mail válido.';
    else if (!maiorDeIdade(iso)) m = 'Atleta menor de 18 anos: a inscrição é feita pela diretoria, com a autorização do responsável legal.';
    setErroNovo(m);
    if (m) return;
    setOcupado(true);
    const r = await chamarApi('POST', `/api/participante/equipes/${id}/atletas`, { nome: a.nome, cpf: a.cpf, nascimento: iso, sexo: a.sexo, email: a.email });
    setOcupado(false);
    if (!r.ok) return setErroNovo(r.erro);
    setNovo({ nome: '', cpf: '', nascimento: '', sexo: 'Masculino', email: '' });
    setAberto(false);
    setAviso(`Convite enviado para ${a.nome}.`);
    await carregar();
  }

  const falta = d.minimo - d.confirmados;
  const tl: { estado: 'done' | 'now' | 'wait' | 'bad'; titulo: string; detalhe: string }[] = [
    { estado: 'done', titulo: 'Equipe enviada', detalhe: dataCurta(d.enviadaEm) },
    { estado: d.confirmados >= d.minimo && d.emAberto === 0 ? 'done' : 'now', titulo: 'Confirmação dos atletas', detalhe: `${d.confirmados} de ${ativos.length} confirmaram` },
    d.situacao === 'Recusada'
      ? { estado: 'bad', titulo: 'Recusada', detalhe: d.motivoRecusa ?? '' }
      : { estado: d.situacao === 'Aprovada' ? 'done' : d.situacao === 'EmAnalise' ? 'now' : 'wait', titulo: 'Análise da diretoria', detalhe: d.situacao === 'Aprovada' ? 'Aprovada' : d.situacao === 'EmAnalise' ? 'Em andamento' : '' },
    ...(d.situacao === 'Recusada' ? [] : [{ estado: (d.situacao === 'Aprovada' ? 'now' : 'wait') as 'now' | 'wait', titulo: 'Atletas efetivados', detalhe: d.situacao === 'Aprovada' ? `${efetivados} de ${d.confirmados} (a taxa de cada um confirma a sua vaga)` : '' }]),
  ];

  return (
    <>
      <p className="small muted"><Link href="/minhas-inscricoes">Minhas inscrições</Link> › Equipe #{d.protocolo}</p>
      <div className="linha entre">
        <div><h1 data-testid="nome-equipe">{d.nome}</h1><p className="muted">{d.campeonato} · {d.ano} · {d.categoria}</p></div>
        <span className={`badge ${CLASSE_EQUIPE[d.situacao]}`} data-testid="situacao-equipe">{ROTULO_EQUIPE[d.situacao]}</span>
      </div>

      {d.situacao === 'AguardandoConfirmacoes' && <p className="aviso info"><b>Faltam confirmações.</b> {d.emAberto > 0 ? `${d.emAberto} atleta${d.emAberto === 1 ? ' ainda não respondeu' : 's ainda não responderam'}. ` : ''}{falta > 0 ? `A equipe precisa de pelo menos ${d.minimo} confirmados (faltam ${falta}). ` : ''}Quem não confirmar em até 14 dias precisa de um novo convite.</p>}
      {d.situacao === 'EmAnalise' && <p className="aviso info">Todos responderam. A diretoria está analisando a equipe. <b>Você não precisa fazer nada agora.</b></p>}
      {d.situacao === 'Aprovada' && <p className="aviso warn"><b>Equipe aprovada!</b> {efetivados} atleta{efetivados === 1 ? '' : 's'} já {efetivados === 1 ? 'está efetivado e no elenco' : 'estão efetivados e no elenco'}. Os demais entram quando a diretoria registrar a taxa de cada um: combine com ela. A equipe segue com quem pagar.</p>}
      {d.situacao === 'Recusada' && <p className="aviso bad" role="alert"><b>A equipe foi recusada.</b> Motivo: {d.motivoRecusa ?? 'não informado'}. Nada foi cobrado.{' '}<Link className="btn sm primary" href="/#ins">Nova inscrição de equipe</Link></p>}
      {aviso && <p className="aviso ok" role="status">{aviso}</p>}

      <div className="card">
        <div className="linha entre">
          <h3>Elenco</h3>
          <div className="chips">
            <span className={`badge ${d.confirmados >= d.minimo ? 'st-Efetivada' : 'st-AguardandoPagamento'}`}>{d.confirmados} confirmados</span>
            <span className="badge">mínimo {d.minimo} · máximo {d.maximo}</span>
            {efetivados > 0 && <span className="badge st-Efetivada">{efetivados} efetivados</span>}
          </div>
        </div>
        <div className="tabela-rolavel">
          <table>
            <thead><tr><th>Atleta</th><th>Confirmação</th><th>Taxa{d.taxa != null ? ` (${brl(d.taxa)})` : ''}</th><th /></tr></thead>
            <tbody>
              {d.atletas.map(a => (
                <tr key={a.id} data-atleta={a.nome}>
                  <td><b>{a.nome}</b>{a.ehResponsavel ? ' (você)' : ''}<div className="small muted">{a.email}</div></td>
                  <td><span className={`badge ${CLASSE_CONVITE[a.status]}`}>{ROTULO_CONVITE[a.status]}</span></td>
                  <td><Taxa a={a} taxa={d.taxa} /></td>
                  <td className="acoes-linha">
                    {abertoParaMudar && a.status !== 'Confirmado' && (
                      <>
                        {(a.status === 'Enviado' || a.status === 'Vencido') && <button className="btn sm" onClick={() => void reenviar(a)}>Reenviar convite</button>}{' '}
                        <button className="btn sm" onClick={() => void remover(a)}>Remover</button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {abertoParaMudar && (
          <div style={{ marginTop: 12 }}>
            {!aberto ? (
              <div className="linha">
                <button className="btn sm" id="adicionar-atleta" disabled={ativos.length >= MAXIMO_DE_ATLETAS} onClick={() => setAberto(true)}>Adicionar atleta</button>
                <span className="small muted">Máximo de {MAXIMO_DE_ATLETAS} atletas.</span>
              </div>
            ) : (
              <fieldset className="novo-atleta">
                <legend>Adicionar atleta</legend>
                <div className="f"><label htmlFor="n-nome">Nome completo</label><input id="n-nome" type="text" value={novo.nome} onChange={e => setNovo({ ...novo, nome: e.target.value })} /></div>
                <div className="g2">
                  <div className="f"><label htmlFor="n-cpf">CPF</label><input id="n-cpf" type="text" inputMode="numeric" value={novo.cpf} onChange={e => setNovo({ ...novo, cpf: mascaraCpf(e.target.value) })} /></div>
                  <div className="f"><label htmlFor="n-nasc">Nascimento</label><CampoData id="n-nasc" valor={novo.nascimento} aoMudar={v => setNovo({ ...novo, nascimento: v })} /></div>
                </div>
                <div className="g2">
                  <div className="f"><label htmlFor="n-sexo">Sexo</label><select id="n-sexo" value={novo.sexo} onChange={e => setNovo({ ...novo, sexo: e.target.value as 'Masculino' | 'Feminino' })}><option>Masculino</option><option>Feminino</option></select></div>
                  <div className="f"><label htmlFor="n-email">E-mail</label><input id="n-email" type="email" value={novo.email} onChange={e => setNovo({ ...novo, email: e.target.value })} /></div>
                </div>
                {erroNovo && <p className="aviso bad" role="alert">{erroNovo}</p>}
                <div className="acoes"><button className="btn" onClick={() => { setAberto(false); setErroNovo(''); }}>Cancelar</button><button className="btn primary" id="enviar-atleta" disabled={ocupado} onClick={() => void adicionar()}>{ocupado ? 'Enviando...' : 'Enviar convite'}</button></div>
              </fieldset>
            )}
          </div>
        )}
      </div>

      <div className="grid g2">
        <div className="card"><h3>Andamento da equipe</h3>
          <ul className="tl">{tl.map(p => <li key={p.titulo} className={p.estado}><b>{p.titulo}</b>{p.detalhe && <span className="small muted">{p.detalhe}</span>}</li>)}</ul>
        </div>
        <div className="card"><h3>Pagamento</h3>
          <p className="small">{d.taxa != null ? <>A taxa é de <b>{brl(d.taxa)} por atleta</b>. </> : 'A taxa é por atleta. '}Cada atleta fica <b>efetivado</b> (e entra no elenco da equipe) quando a diretoria registra a taxa dele: a equipe não precisa esperar todos.</p>
          <p className="small muted">Combine o pagamento com a diretoria: <Link href="/contato">contato</Link>.</p>
          <hr /><p className="small muted">Torneio aberto: participar não torna os atletas associados.</p>
        </div>
      </div>
    </>
  );
}

'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import CampoData from '@/components/CampoData';
import { type AtletaDoElenco, ATLETAS_PARA_RODIZIO, MAXIMO_DE_ATLETAS, MINIMO_DE_ATLETAS } from '@/lib/equipe';
import { brl } from '@/lib/detalhe';
import { cpfValido, dataCurta, dataParaIso, mascaraCpf, mascaraTelefone, maiorDeIdade, type Preparo } from '@/lib/inscricao';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

const NOMES = ['Responsável', 'Equipe', 'Elenco', 'Revisão'];

export interface DadosDoTorneio {
  temporadaId: string;
  campeonato: string;
  ano: number;
  fimInscricoes: string;
  taxa: number | null;
  categorias: { id: string; nome: string; idadeMinima: number; sexo: 'Masculino' | 'Feminino' | null }[];
}

/** Código do envio: gerado uma vez por temporada e guardado na aba, para o reenvio (recarregar, duplo clique) não duplicar a equipe. */
function codigoDeEnvio(temporadaId: string): string {
  const chave = `sismaster.equipe.codigo.${temporadaId}`;
  try {
    const atual = sessionStorage.getItem(chave);
    if (atual) return atual;
    const novo = crypto.randomUUID();
    sessionStorage.setItem(chave, novo);
    return novo;
  } catch {
    return crypto.randomUUID();
  }
}
const limparCodigo = (temporadaId: string) => { try { sessionStorage.removeItem(`sismaster.equipe.codigo.${temporadaId}`); } catch { /* ok */ } };

export default function AssistenteDeEquipe({ t }: { t: DadosDoTorneio }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [passo, setPasso] = useState(1);
  const [erro, setErro] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [enviada, setEnviada] = useState<{ id: string; protocolo: string } | null>(null);

  // 1 responsável
  const [telefone, setTelefone] = useState('');
  const [joga, setJoga] = useState(false);
  const [eu, setEu] = useState({ cpf: '', nascimento: '', sexo: 'Masculino' as 'Masculino' | 'Feminino' });
  // 2 equipe
  const [nome, setNome] = useState('');
  const [categoriaId, setCategoriaId] = useState(t.categorias[0]?.id ?? '');
  // 3 elenco
  const [elenco, setElenco] = useState<AtletaDoElenco[]>([]);
  const [novo, setNovo] = useState<AtletaDoElenco>({ nome: '', cpf: '', nascimento: '', sexo: 'Masculino', email: '' });
  const [erroAtleta, setErroAtleta] = useState('');

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (!s) return;
    // Já com cadastro ligado à conta: o telefone, o nascimento e o sexo vêm dele.
    void chamarApi<Preparo>('GET', `/api/participante/temporadas/${t.temporadaId}/inscricao/preparo`).then(r => {
      if (!r.ok || !r.dados.pessoa) return;
      const p = r.dados.pessoa;
      setTelefone(p.telefone ?? '');
      setEu(e => ({ ...e, nascimento: p.nascimento.split('-').reverse().join('/'), sexo: p.sexo }));
    });
  }, [t.temporadaId]);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Inscrição de equipe</h1>
        <p className="muted">Para inscrever uma equipe você precisa de uma conta: você é o responsável e o contato da diretoria.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent(`/inscricao/${t.temporadaId}/equipe`)}`}>Entrar ou criar conta</Link>
      </div>
    );
  }

  const categoria = t.categorias.find(c => c.id === categoriaId);
  const total = elenco.length + (joga ? 1 : 0);
  const titulo = `${t.campeonato} ${t.ano}`;

  /** Idade na temporada e avisos de elegibilidade (a diretoria decide; o responsável só é avisado). */
  const idade = (br: string) => { const iso = dataParaIso(br); return iso ? t.ano - Number(iso.slice(0, 4)) : null; };
  const avisos = (a: { nascimento: string; sexo: string }) => {
    const r: string[] = [];
    const i = idade(a.nascimento);
    if (categoria && i !== null && i < categoria.idadeMinima) r.push(`${i} anos (mínimo ${categoria.idadeMinima})`);
    if (categoria?.sexo && a.sexo !== categoria.sexo) r.push(`categoria ${categoria.sexo.toLowerCase()}`);
    return r;
  };

  function validarResponsavel(): string {
    if (telefone.replace(/\D/g, '').length < 10) return 'Informe o telefone com DDD.';
    if (joga) {
      if (!cpfValido(eu.cpf)) return 'Informe o seu CPF (11 números válidos).';
      const iso = dataParaIso(eu.nascimento);
      if (!iso) return 'Informe a sua data de nascimento (DD/MM/AAAA).';
      if (!maiorDeIdade(iso)) return 'Atleta menor de 18 anos: a inscrição é feita pela diretoria, com a autorização do responsável legal. Fale com a diretoria.';
    }
    return '';
  }

  function adicionarAtleta() {
    const a = { ...novo, nome: novo.nome.trim(), email: novo.email.trim() };
    const iso = dataParaIso(a.nascimento);
    let m = '';
    if (a.nome.length < 3) m = 'Informe o nome completo.';
    else if (!cpfValido(a.cpf)) m = 'CPF inválido. Confira os 11 números.';
    else if (!iso) m = 'Informe a data de nascimento (DD/MM/AAAA).';
    else if (!a.email.includes('@')) m = 'Informe um e-mail válido.';
    else if (elenco.some(x => x.cpf === a.cpf) || (joga && eu.cpf === a.cpf)) m = 'Este CPF já está no elenco.';
    else if (elenco.some(x => x.email.toLowerCase() === a.email.toLowerCase()) || (joga && sessao!.usuario.email.toLowerCase() === a.email.toLowerCase())) m = 'Este e-mail já está no elenco: cada atleta precisa do próprio e-mail.';
    else if (!maiorDeIdade(iso!)) m = 'Atleta menor de 18 anos: a inscrição é feita pela diretoria, com a autorização do responsável legal. Fale com a diretoria.';
    else if (total >= MAXIMO_DE_ATLETAS) m = `O elenco já tem ${MAXIMO_DE_ATLETAS} atletas (o máximo).`;
    setErroAtleta(m);
    setErro('');
    if (m) return;
    setElenco(l => [...l, a]);
    setNovo({ nome: '', cpf: '', nascimento: '', sexo: 'Masculino', email: '' });
  }

  function continuar() {
    setErro('');
    let m = '';
    if (passo === 1) m = validarResponsavel();
    if (passo === 2 && nome.trim().length < 3) m = 'Informe o nome da equipe (mínimo 3 letras).';
    if (passo === 3 && total < MINIMO_DE_ATLETAS) m = `A equipe precisa de pelo menos ${MINIMO_DE_ATLETAS} atletas (hoje: ${total}).`;
    if (m) return setErro(m);
    setPasso(p => p + 1);
  }

  async function enviar() {
    setErro('');
    setOcupado(true);
    const iso = dataParaIso(eu.nascimento);
    const r = await chamarApi<{ id: string; protocolo: string }>('POST', '/api/participante/equipes', {
      temporadaId: t.temporadaId,
      temporadaCategoriaId: categoriaId,
      codigoDeEnvio: codigoDeEnvio(t.temporadaId),
      nome: nome.trim(),
      responsavelNome: sessao!.usuario.nome,
      responsavelTelefone: telefone.trim(),
      responsavelJoga: joga,
      responsavel: joga ? { nome: sessao!.usuario.nome, cpf: eu.cpf, nascimento: iso, sexo: eu.sexo, email: sessao!.usuario.email } : null,
      atletas: elenco.map(a => ({ nome: a.nome, cpf: a.cpf, nascimento: dataParaIso(a.nascimento), sexo: a.sexo, email: a.email })),
    });
    setOcupado(false);
    if (!r.ok) return setErro(r.erro);
    limparCodigo(t.temporadaId);
    setEnviada(r.dados);
  }

  if (enviada) {
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✅</div>
        <h1>Equipe enviada!</h1>
        <div className="proto" data-testid="protocolo-equipe">Protocolo #{enviada.protocolo}</div>
        <p className="muted">{total} convite{total === 1 ? '' : 's'} enviado{total === 1 ? '' : 's'} por e-mail. Guarde o número do protocolo.</p>
        <p>Cada atleta confirma a própria participação pelo link do e-mail. Quando pelo menos {MINIMO_DE_ATLETAS} confirmarem, a diretoria analisa a equipe e você é avisado por e-mail.</p>
        <Link className="btn primary block" href={`/minhas-inscricoes/equipes/${enviada.id}`}>Acompanhar minha equipe</Link>
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  return (
    <>
      <div className="passos" aria-label="Passos da inscrição de equipe">
        {NOMES.map((n, i) => <span key={n} className={i + 1 < passo ? 'ok' : i + 1 === passo ? 'on' : ''}>{i + 1} {n}</span>)}
      </div>
      <div className="card conta">
        <p className="small muted" style={{ margin: '0 0 6px' }}>
          Inscrição de equipe · {titulo} · até {dataCurta(t.fimInscricoes)}{t.taxa != null ? ` · taxa de ${brl(t.taxa)} por atleta` : ''}
        </p>
        {erro && <p className="aviso bad" role="alert">{erro}</p>}

        {passo === 1 && (
          <>
            <h1>Responsável pela equipe</h1>
            <p className="muted">Você monta o elenco, acompanha a inscrição e é o contato da diretoria.</p>
            <div className="g2">
              <div className="f"><label htmlFor="e-resp">Nome</label><input id="e-resp" type="text" value={sessao.usuario.nome} readOnly /></div>
              <div className="f"><label htmlFor="e-mail">E-mail da conta</label><input id="e-mail" type="email" value={sessao.usuario.email} readOnly /></div>
            </div>
            <div className="f"><label htmlFor="e-tel">Telefone / WhatsApp</label><input id="e-tel" type="text" inputMode="tel" value={telefone} onChange={e => setTelefone(mascaraTelefone(e.target.value))} /><span className="hint">A diretoria fala com você por aqui.</span></div>
            <label className={`cat ${joga ? 'on' : ''}`}>
              <input type="checkbox" id="e-joga" checked={joga} onChange={e => setJoga(e.target.checked)} />
              <span><b>Também vou jogar nesta equipe</b><small>Você entra no elenco como atleta e também confirma o seu convite, com as suas autorizações.</small></span>
            </label>
            {joga && (
              <>
                <div className="g2">
                  <div className="f"><label htmlFor="e-cpf">Seu CPF</label><input id="e-cpf" type="text" inputMode="numeric" value={eu.cpf} onChange={e => setEu({ ...eu, cpf: mascaraCpf(e.target.value) })} /></div>
                  <div className="f"><label htmlFor="e-nasc">Seu nascimento</label><CampoData id="e-nasc" valor={eu.nascimento} aoMudar={v => setEu({ ...eu, nascimento: v })} /></div>
                </div>
                <div className="f"><label htmlFor="e-sexo">Seu sexo</label><select id="e-sexo" value={eu.sexo} onChange={e => setEu({ ...eu, sexo: e.target.value as 'Masculino' | 'Feminino' })}><option>Masculino</option><option>Feminino</option></select></div>
              </>
            )}
            <div className="acoes"><Link className="btn" href={`/inscricao/${t.temporadaId}`}>Voltar</Link><button className="btn primary" onClick={continuar}>Continuar</button></div>
          </>
        )}

        {passo === 2 && (
          <>
            <h1>Equipe</h1>
            <div className="f"><label htmlFor="e-nome">Nome da equipe</label><input id="e-nome" type="text" maxLength={100} placeholder="Ex.: Tubarões do Verão" value={nome} onChange={e => setNome(e.target.value)} /><span className="hint">Não pode repetir o nome de outra equipe do torneio.</span></div>
            <div className="f"><label htmlFor="e-cat">Categoria</label>
              <select id="e-cat" value={categoriaId} onChange={e => setCategoriaId(e.target.value)}>{t.categorias.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}</select></div>
            {categoria && <p className="aviso info"><b>{categoria.nome}:</b> a partir de {categoria.idadeMinima} anos (idade em {t.ano}) · {categoria.sexo ? `só ${categoria.sexo.toLowerCase()}` : 'categoria mista'} · de {MINIMO_DE_ATLETAS} a {MAXIMO_DE_ATLETAS} atletas ({ATLETAS_PARA_RODIZIO} permitem o rodízio).</p>}
            <div className="acoes"><button className="btn" onClick={() => setPasso(1)}>Voltar</button><button className="btn primary" onClick={continuar}>Continuar</button></div>
          </>
        )}

        {passo === 3 && (
          <>
            <h1>Elenco</h1>
            <p className="muted">
              <b data-testid="contagem">{total} atleta{total === 1 ? '' : 's'}</b> · mínimo {MINIMO_DE_ATLETAS} · máximo {MAXIMO_DE_ATLETAS} · {total >= ATLETAS_PARA_RODIZIO ? '✔ rodízio' : `rodízio exige ${ATLETAS_PARA_RODIZIO}`}
              {joga && ' (você já está contado)'}
            </p>
            <fieldset className="novo-atleta">
              <legend>Adicionar atleta</legend>
              <div className="f"><label htmlFor="a-nome">Nome completo</label><input id="a-nome" type="text" value={novo.nome} onChange={e => setNovo({ ...novo, nome: e.target.value })} /></div>
              <div className="g2">
                <div className="f"><label htmlFor="a-cpf">CPF</label><input id="a-cpf" type="text" inputMode="numeric" value={novo.cpf} onChange={e => setNovo({ ...novo, cpf: mascaraCpf(e.target.value) })} /></div>
                <div className="f"><label htmlFor="a-nasc">Nascimento</label><CampoData id="a-nasc" valor={novo.nascimento} aoMudar={v => setNovo({ ...novo, nascimento: v })} /></div>
              </div>
              <div className="g2">
                <div className="f"><label htmlFor="a-sexo">Sexo</label><select id="a-sexo" value={novo.sexo} onChange={e => setNovo({ ...novo, sexo: e.target.value as 'Masculino' | 'Feminino' })}><option>Masculino</option><option>Feminino</option></select></div>
                <div className="f"><label htmlFor="a-email">E-mail</label><input id="a-email" type="email" value={novo.email} onChange={e => setNovo({ ...novo, email: e.target.value })} /><span className="hint">Ele recebe o convite para confirmar.</span></div>
              </div>
              {erroAtleta && <p className="aviso bad" role="alert">{erroAtleta}</p>}
              <button type="button" className="btn primary" id="a-adicionar" onClick={adicionarAtleta}>Adicionar ao elenco</button>
            </fieldset>

            {elenco.length > 0 && (
              <div className="tabela-rolavel">
                <table>
                  <thead><tr><th>#</th><th>Atleta</th><th>Contato</th><th /></tr></thead>
                  <tbody>
                    {elenco.map((a, i) => {
                      const av = avisos(a);
                      return (
                        <tr key={a.cpf}>
                          <td>{i + 1 + (joga ? 1 : 0)}</td>
                          <td><b>{a.nome}</b>{av.length > 0 && <span className="tag">fora do esperado</span>}
                            <div className="small muted">{a.cpf} · {a.nascimento}{idade(a.nascimento) !== null ? ` (${idade(a.nascimento)} anos)` : ''}{av.length > 0 ? ` · ${av.join(', ')}` : ''}</div></td>
                          <td className="small">{a.email}</td>
                          <td><button className="btn sm" onClick={() => setElenco(l => l.filter(x => x.cpf !== a.cpf))}>Remover</button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
            <p className="aviso info">Cada atleta recebe um e-mail para <b>confirmar a participação</b> e aceitar, ele mesmo, o uso de dados e de imagem. Você não autoriza por ele.</p>
            <div className="acoes"><button className="btn" onClick={() => setPasso(2)}>Voltar</button><button className="btn primary" onClick={continuar}>Continuar</button></div>
          </>
        )}

        {passo === 4 && (
          <>
            <h1>Revise e envie</h1>
            <dl className="rev">
              <dt>Equipe</dt><dd>{nome.trim()}</dd>
              <dt>Torneio</dt><dd>{titulo}</dd>
              <dt>Categoria</dt><dd>{categoria?.nome}</dd>
              <dt>Responsável</dt><dd>{sessao.usuario.nome} · {telefone}</dd>
              <dt>Elenco</dt><dd>{total} atletas{joga ? ' (incluindo o responsável)' : ''}</dd>
            </dl>
            <ul className="small">
              {joga && <li>{sessao.usuario.nome} (responsável) — {sessao.usuario.email}</li>}
              {elenco.map(a => <li key={a.cpf}>{a.nome} — {a.email}{avisos(a).length > 0 && <span className="tag">fora do esperado</span>}</li>)}
            </ul>
            {elenco.some(a => avisos(a).length > 0) && <p className="small muted">Atletas fora do esperado seguem no pedido; a diretoria decide.</p>}
            <p className="aviso info">
              Ao enviar, os convites saem por e-mail e a diretoria analisa a equipe. <b>Nada é cobrado agora.</b>
              {t.taxa != null && <> A taxa de <b>{brl(t.taxa)} é por atleta</b> e a diretoria a registra atleta a atleta:</>} cada um fica efetivado, e entra no elenco, quando a sua taxa é registrada. Quem não confirmar o convite em 14 dias sai do elenco.
            </p>
            <p className="aviso warn"><b>Torneio aberto:</b> a inscrição vale só para este torneio e não torna os atletas associados.</p>
            <div className="acoes"><button className="btn" onClick={() => setPasso(3)}>Voltar</button><button className="btn primary" onClick={enviar} disabled={ocupado}>{ocupado ? 'Enviando...' : 'Enviar equipe'}</button></div>
          </>
        )}
      </div>
    </>
  );
}

'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import {
  type CategoriaDoFormulario, type InscricaoEnviada, type PessoaDoSite, type Preparo, type ResultadoDoVinculo,
  POSICOES, cpfValido, dataParaIso, foraDoEsperado, isoParaBr, maiorDeIdade, mascaraCpf, mascaraTelefone,
} from '@/lib/inscricao';
import CampoData from '@/components/CampoData';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

const NOMES = ['Seus dados', 'Ficha', 'Categorias', 'Autorizações', 'Revisão'];

type Ficha = { altura: string; peso: string; posicao: string; plano: 'nao' | 'sim'; nomePlano: string };

/** Código do envio: gerado uma vez por temporada e guardado na aba, para o reenvio (recarregar, duplo clique) não duplicar a inscrição. */
function codigoDeEnvio(temporadaId: string): string {
  const chave = `sismaster.inscricao.codigo.${temporadaId}`;
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
const limparCodigo = (temporadaId: string) => { try { sessionStorage.removeItem(`sismaster.inscricao.codigo.${temporadaId}`); } catch { /* ok */ } };

export default function Assistente({ temporadaId, versaoTermos }: { temporadaId: string; versaoTermos: string }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [preparo, setPreparo] = useState<Preparo | null>(null);
  const [erroCarga, setErroCarga] = useState('');
  const [passo, setPasso] = useState(1);
  const [pessoa, setPessoa] = useState<PessoaDoSite | null>(null);
  const [bloqueio, setBloqueio] = useState<'Bloqueada' | 'Menor' | null>(null);

  // passo 1 (cadastro novo)
  const [nome, setNome] = useState('');
  const [cpf, setCpf] = useState('');
  const [nasc, setNasc] = useState('');
  const [sexo, setSexo] = useState<'Masculino' | 'Feminino'>('Masculino');
  const [telefone, setTelefone] = useState('');
  // passo 2
  const [ficha, setFicha] = useState<Ficha>({ altura: '', peso: '', posicao: '', plano: 'nao', nomePlano: '' });
  // passo 3 e 4
  const [escolhidas, setEscolhidas] = useState<Set<string>>(new Set());
  const [aceiteDados, setAceiteDados] = useState(false);
  const [aceiteImagem, setAceiteImagem] = useState(false);

  const [erro, setErro] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [enviada, setEnviada] = useState<InscricaoEnviada | null>(null);

  const carregar = useCallback(async () => {
    const r = await chamarApi<Preparo>('GET', `/api/participante/temporadas/${temporadaId}/inscricao/preparo`);
    if (!r.ok) { setErroCarga(r.erro); if (r.expirou) setSessao(null); return; }
    const p = r.dados;
    setPreparo(p);
    if (p.pessoa) {
      setPessoa(p.pessoa);
      setTelefone(p.pessoa.telefone ?? '');
      if (p.pessoa.menor) setBloqueio('Menor');
    }
    if (p.ultimaFicha) {
      const u = p.ultimaFicha;
      setFicha({ altura: u.alturaCm?.toString() ?? '', peso: u.pesoKg?.toString() ?? '', posicao: u.posicao ?? '', plano: u.possuiPlanoSaude ? 'sim' : 'nao', nomePlano: u.nomePlanoSaude ?? '' });
    }
  }, [temporadaId]);

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (s) { setNome(s.usuario.nome); void carregar(); }
  }, [carregar]);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Inscrição</h1>
        <p className="muted">Para se inscrever você precisa de uma conta.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent(`/inscricao/${temporadaId}`)}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  if (erroCarga) return <div className="card conta"><p className="aviso bad" role="alert">{erroCarga}</p><Link className="btn block" href="/">Voltar ao início</Link></div>;
  if (!preparo) return <div className="card conta centro"><p>Carregando…</p></div>;

  const t = preparo.temporada;
  const titulo = `${t.campeonato} · ${t.ano}`;

  if (!t.abertas) {
    return (
      <div className="card conta centro">
        <h1>Inscrições encerradas</h1>
        <p>{titulo}</p>
        <p className="muted">As inscrições desta temporada não estão abertas no momento.</p>
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  // ---------- passo 1: dados ----------

  async function continuarDados() {
    setErro('');
    if (pessoa) {
      // cadastro já ligado: só o telefone pode mudar
      setOcupado(true);
      const r = await chamarApi<ResultadoDoVinculo>('POST', '/api/participante/vinculo', { nome: pessoa.nome, cpf: '', nascimento: pessoa.nascimento, sexo: pessoa.sexo, telefone });
      setOcupado(false);
      if (!r.ok) return setErro(r.erro);
      if (r.dados.situacao === 'Menor') return setBloqueio('Menor');
      if (r.dados.pessoa) setPessoa(r.dados.pessoa);
      return setPasso(2);
    }
    if (nome.trim().length < 3) return setErro('Informe o nome completo.');
    if (!cpfValido(cpf)) return setErro('CPF inválido. Confira os 11 números.');
    const iso = dataParaIso(nasc);
    if (!iso) return setErro('Informe uma data de nascimento válida (DD/MM/AAAA).');
    if (!maiorDeIdade(iso)) return setBloqueio('Menor');
    setOcupado(true);
    const r = await chamarApi<ResultadoDoVinculo>('POST', '/api/participante/vinculo', { nome: nome.trim(), cpf, nascimento: iso, sexo, telefone: telefone.trim() || null });
    setOcupado(false);
    if (!r.ok) return setErro(r.erro);
    if (r.dados.situacao !== 'Vinculada' || !r.dados.pessoa) return setBloqueio(r.dados.situacao === 'Menor' ? 'Menor' : 'Bloqueada');
    setPessoa(r.dados.pessoa);
    setPasso(2);
  }

  // ---------- passo 2: ficha ----------

  function continuarFicha() {
    setErro('');
    const a = ficha.altura.trim() === '' ? null : Number(ficha.altura);
    const p = ficha.peso.trim() === '' ? null : Number(ficha.peso);
    if (a !== null && (!Number.isInteger(a) || a < 100 || a > 260)) return setErro('A altura deve estar entre 100 e 260 cm.');
    if (p !== null && (!Number.isInteger(p) || p < 30 || p > 250)) return setErro('O peso deve estar entre 30 e 250 kg.');
    if (ficha.plano === 'sim' && !ficha.nomePlano.trim()) return setErro('Informe o nome do plano de saúde.');
    setPasso(3);
  }

  // ---------- passo 3: categorias ----------

  const alertas = (c: CategoriaDoFormulario) => (pessoa ? foraDoEsperado(c, pessoa.nascimento, pessoa.sexo, t.ano) : []);

  function continuarCategorias() {
    setErro('');
    if (escolhidas.size === 0) return setErro('Escolha ao menos uma categoria.');
    setPasso(4);
  }

  // ---------- envio ----------

  async function enviar() {
    setErro('');
    setOcupado(true);
    const r = await chamarApi<InscricaoEnviada>('POST', '/api/participante/inscricoes', {
      temporadaId,
      codigoDeEnvio: codigoDeEnvio(temporadaId),
      alturaCm: ficha.altura.trim() === '' ? null : Number(ficha.altura),
      pesoKg: ficha.peso.trim() === '' ? null : Number(ficha.peso),
      posicao: ficha.posicao || null,
      possuiPlanoSaude: ficha.plano === 'sim',
      nomePlanoSaude: ficha.plano === 'sim' ? ficha.nomePlano.trim() : null,
      aceiteDados, aceiteImagem, versaoTermos,
      categoriaIds: [...escolhidas],
    });
    setOcupado(false);
    if (!r.ok) return setErro(r.erro);
    limparCodigo(temporadaId);
    setEnviada(r.dados);
  }

  // ---------- telas ----------

  if (enviada) {
    const quando = new Date(enviada.enviadaEm);
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✅</div>
        <h1>Inscrição enviada!</h1>
        <div className="proto" data-testid="protocolo">Protocolo #{enviada.protocolo}</div>
        <p className="muted">Enviada em {quando.toLocaleDateString('pt-BR')} às {quando.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}. Guarde o número do protocolo.</p>
        <p>Enviamos uma cópia para <b>{sessao.usuario.email}</b>. A diretoria vai analisar o pedido; você será avisado a cada mudança de situação.</p>
        <Link className="btn primary block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  if (bloqueio) {
    return (
      <div className="card conta">
        <h1>Inscrição — {titulo}</h1>
        {bloqueio === 'Menor' ? (
          <p className="aviso warn"><b>Inscrição de menor de 18 anos</b> é feita pela diretoria, com a autorização do responsável. Fale com a associação (veja em <Link href="/contato">Contato</Link>).</p>
        ) : (
          <p className="aviso bad" role="alert"><b>Não conseguimos continuar a inscrição online.</b> Já existe um cadastro com este CPF que não está ligado a esta conta. Fale com a diretoria (veja em <Link href="/contato">Contato</Link>): ela faz a ligação e você volta para concluir. Por segurança, não mostramos dados do cadastro.</p>
        )}
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  return (
    <>
      <div className="passos" aria-label="Passos da inscrição">
        {NOMES.map((n, i) => <span key={n} className={i + 1 < passo ? 'ok' : i + 1 === passo ? 'on' : ''}>{i + 1} {n}</span>)}
      </div>
      <div className="card conta">
        <p className="small muted" style={{ margin: '0 0 6px' }}>Inscrição · {titulo} · até {t.dataFimInscricoes.slice(0, 10).split('-').reverse().join('/')}</p>
        {erro && <p className="aviso bad" role="alert">{erro}</p>}

        {passo === 1 && (
          <>
            <h1>Seus dados</h1>
            {pessoa ? (
              <>
                <p className="aviso info">Encontramos o seu cadastro na associação. Nome, CPF, nascimento e sexo vêm do cadastro e não mudam aqui; para corrigir, fale com a diretoria. Só o telefone pode ser atualizado.</p>
                <div className="f"><label htmlFor="r-nome">Nome completo</label><input id="r-nome" type="text" value={pessoa.nome} readOnly /></div>
                <div className="g2">
                  <div className="f"><label htmlFor="r-cpf">CPF</label><input id="r-cpf" type="text" value={pessoa.cpfMascarado} readOnly /></div>
                  <div className="f"><label htmlFor="r-nasc">Data de nascimento</label><input id="r-nasc" type="text" value={isoParaBr(pessoa.nascimento)} readOnly /></div>
                </div>
                <div className="g2">
                  <div className="f"><label htmlFor="r-sexo">Sexo</label><input id="r-sexo" type="text" value={pessoa.sexo} readOnly /></div>
                  <div className="f"><label htmlFor="a-tel">Telefone / WhatsApp</label><input id="a-tel" type="text" inputMode="tel" value={telefone} onChange={e => setTelefone(mascaraTelefone(e.target.value))} /></div>
                </div>
              </>
            ) : (
              <>
                <p className="muted">Confira seus dados: eles identificam você nas súmulas e nas categorias.</p>
                <div className="f"><label htmlFor="a-nome">Nome completo</label><input id="a-nome" type="text" autoComplete="name" value={nome} onChange={e => setNome(e.target.value)} /></div>
                <div className="g2">
                  <div className="f"><label htmlFor="a-cpf">CPF</label><input id="a-cpf" type="text" inputMode="numeric" value={cpf} onChange={e => setCpf(mascaraCpf(e.target.value))} /><span className="hint">Usamos o CPF para reconhecer seu cadastro e evitar duplicidade.</span></div>
                  <div className="f"><label htmlFor="a-nasc">Data de nascimento</label><CampoData id="a-nasc" valor={nasc} aoMudar={setNasc} /></div>
                </div>
                <div className="g2">
                  <div className="f"><label htmlFor="a-sexo">Sexo</label><select id="a-sexo" value={sexo} onChange={e => setSexo(e.target.value as 'Masculino' | 'Feminino')}><option>Masculino</option><option>Feminino</option></select><span className="hint">Usado para a elegibilidade nas categorias.</span></div>
                  <div className="f"><label htmlFor="a-tel">Telefone / WhatsApp</label><input id="a-tel" type="text" inputMode="tel" value={telefone} onChange={e => setTelefone(mascaraTelefone(e.target.value))} /></div>
                </div>
              </>
            )}
            <div className="f"><label htmlFor="a-email">E-mail da conta</label><input id="a-email" type="email" value={sessao.usuario.email} readOnly /><span className="hint">É o e-mail da sua conta; ele não muda aqui.</span></div>
            <div className="acoes"><Link className="btn" href="/">Cancelar</Link><button className="btn primary" onClick={continuarDados} disabled={ocupado}>{ocupado ? 'Verificando...' : 'Continuar'}</button></div>
          </>
        )}

        {passo === 2 && (
          <>
            <h1>Ficha esportiva</h1>
            <p className="muted">{preparo.ultimaFicha ? 'Preenchemos com a ficha da sua última inscrição: confirme ou ajuste.' : 'Tudo opcional, exceto o plano de saúde. Ajuda a diretoria e a mesa de jogo.'}</p>
            <div className="g2">
              <div className="f"><label htmlFor="a-alt">Altura (cm)</label><input id="a-alt" type="text" inputMode="numeric" value={ficha.altura} onChange={e => setFicha({ ...ficha, altura: e.target.value.replace(/\D/g, '').slice(0, 3) })} /></div>
              <div className="f"><label htmlFor="a-peso">Peso (kg)</label><input id="a-peso" type="text" inputMode="numeric" value={ficha.peso} onChange={e => setFicha({ ...ficha, peso: e.target.value.replace(/\D/g, '').slice(0, 3) })} /></div>
            </div>
            <div className="f"><label htmlFor="a-pos">Posição</label>
              <select id="a-pos" value={ficha.posicao} onChange={e => setFicha({ ...ficha, posicao: e.target.value })}><option value="">Não informar</option>{POSICOES.map(p => <option key={p}>{p}</option>)}</select></div>
            <div className="f"><label htmlFor="a-plano">Possui plano de saúde?</label>
              <select id="a-plano" value={ficha.plano} onChange={e => setFicha({ ...ficha, plano: e.target.value as 'nao' | 'sim' })}><option value="nao">Não</option><option value="sim">Sim</option></select></div>
            {ficha.plano === 'sim' && <div className="f"><label htmlFor="a-nplano">Qual plano?</label><input id="a-nplano" type="text" maxLength={100} value={ficha.nomePlano} onChange={e => setFicha({ ...ficha, nomePlano: e.target.value })} /></div>}
            <div className="acoes"><button className="btn" onClick={() => setPasso(1)}>Voltar</button><button className="btn primary" onClick={continuarFicha}>Continuar</button></div>
          </>
        )}

        {passo === 3 && pessoa && (
          <>
            <h1>Categorias</h1>
            <p className="muted">Idade considerada na temporada: <b>{t.ano - Number(pessoa.nascimento.slice(0, 4))} anos</b> ({t.ano} − {pessoa.nascimento.slice(0, 4)}). Você pode pedir mais de uma categoria.</p>
            {preparo.categorias.map(c => {
              const motivos = alertas(c);
              const ja = c.jaPedida !== null;
              return (
                <label key={c.id} className={`cat ${ja ? 'dis' : ''}`}>
                  <input type="checkbox" disabled={ja} checked={escolhidas.has(c.id)}
                    onChange={e => setEscolhidas(prev => { const n = new Set(prev); if (e.target.checked) n.add(c.id); else n.delete(c.id); return n; })} />
                  <span>
                    <b>{c.nome}{motivos.length > 0 && !ja && <span className="tag">fora do esperado</span>}{ja && <span className="tag dis">já pedida</span>}</b>
                    <small>{c.sexo ?? 'Misto'} · a partir de {c.idadeMinima} anos{motivos.length > 0 && !ja ? ` — ${motivos.join('; ')}: você pode pedir; a diretoria decide` : ''}</small>
                    {ja && <small>Você já tem um pedido ativo nesta categoria (protocolo #{c.jaPedida}).</small>}
                  </span>
                </label>
              );
            })}
            <div className="acoes"><button className="btn" onClick={() => setPasso(2)}>Voltar</button><button className="btn primary" onClick={continuarCategorias}>Continuar</button></div>
          </>
        )}

        {passo === 4 && (
          <>
            <h1>Autorizações</h1>
            <p className="muted">As duas são necessárias para participar. Ficam registradas com data, hora e a versão do texto ({versaoTermos}).</p>
            <label className="cat"><input type="checkbox" checked={aceiteDados} onChange={e => setAceiteDados(e.target.checked)} />
              <span><b>Tratamento dos meus dados pessoais (LGPD)</b><small>Autorizo a associação a tratar os dados informados para analisar a inscrição, organizar campeonatos, súmulas e estatísticas, e entrar em contato comigo. <Link href="/privacidade" target="_blank">Política de privacidade</Link></small></span></label>
            <label className="cat"><input type="checkbox" checked={aceiteImagem} onChange={e => setAceiteImagem(e.target.checked)} />
              <span><b>Uso da minha imagem e dados para divulgação</b><small>Autorizo a divulgar meu nome, fotos, vídeos, resultados e estatísticas esportivas no site e nas redes da associação, no contexto esportivo e institucional. <Link href="/termos" target="_blank">Termos de uso</Link></small></span></label>
            <div className="acoes"><button className="btn" onClick={() => setPasso(3)}>Voltar</button>
              <button className="btn primary" onClick={() => { setErro(''); if (!aceiteDados || !aceiteImagem) return setErro('Marque as duas autorizações para continuar.'); setPasso(5); }}>Continuar</button></div>
          </>
        )}

        {passo === 5 && pessoa && (
          <>
            <h1>Revise e envie</h1>
            <dl className="rev">
              <dt>Campeonato</dt><dd>{titulo}</dd>
              <dt>Nome</dt><dd>{pessoa.nome}</dd>
              <dt>CPF</dt><dd>{pessoa.cpfMascarado}</dd>
              <dt>Nascimento</dt><dd>{isoParaBr(pessoa.nascimento)} ({t.ano - Number(pessoa.nascimento.slice(0, 4))} anos em {t.ano})</dd>
              <dt>Contato</dt><dd>{sessao.usuario.email}{telefone ? ` · ${telefone}` : ''}</dd>
              <dt>Ficha</dt><dd>{[ficha.altura && `${ficha.altura} cm`, ficha.peso && `${ficha.peso} kg`, ficha.posicao, ficha.plano === 'sim' ? `Plano: ${ficha.nomePlano}` : 'Sem plano de saúde'].filter(Boolean).join(' · ')}</dd>
              <dt>Categorias</dt><dd>{preparo.categorias.filter(c => escolhidas.has(c.id)).map(c => c.nome).join(', ')}</dd>
              <dt>Autorizações</dt><dd>Dados (LGPD) ✓ · Imagem e divulgação ✓</dd>
            </dl>
            {preparo.categorias.filter(c => escolhidas.has(c.id)).flatMap(c => alertas(c).map(m => <p className="aviso warn" key={c.id}>{c.nome}: {m}. A diretoria decide se aceita.</p>))}
            <p className="muted small">Ao enviar, a diretoria analisa o pedido. <b>Nada é cobrado agora</b>: se for aprovado, você recebe as instruções de pagamento da taxa pela associação.</p>
            <div className="acoes"><button className="btn" onClick={() => setPasso(4)}>Voltar</button><button className="btn primary" onClick={enviar} disabled={ocupado}>{ocupado ? 'Enviando...' : 'Enviar inscrição'}</button></div>
          </>
        )}
      </div>
    </>
  );
}

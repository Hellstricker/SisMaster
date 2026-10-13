'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import { isoParaBr, mascaraTelefone } from '@/lib/inscricao';
import { dataEHora } from '@/lib/detalhe';
import { chamarApi, iniciarSessao, lerSessao, sair, type SessaoDoSite } from '@/lib/sessao';
import CampoSenha, { senhaValida } from './CampoSenha';

interface DadosDaConta {
  pessoa: { nome: string; cpfMascarado: string; nascimento: string; sexo: string; telefone: string | null; email: string } | null;
  autorizacoes: { protocolo: string | null; campeonato: string | null; ano: number | null; dados: string; imagem: string | null; versao: string | null }[];
}

type Aba = 'dados' | 'senha' | 'autorizacoes';
const ABAS: { id: Aba; nome: string }[] = [{ id: 'dados', nome: 'Dados pessoais' }, { id: 'senha', nome: 'Senha' }, { id: 'autorizacoes', nome: 'Autorizações' }];

/** Minha conta (I8): dados do cadastro (só o telefone muda), troca de senha e as autorizações dadas em cada inscrição. */
export default function MinhaConta() {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [dados, setDados] = useState<DadosDaConta | null>(null);
  const [erroCarga, setErroCarga] = useState('');
  const [aba, setAba] = useState<Aba>('dados');

  const [telefone, setTelefone] = useState('');
  const [msgDados, setMsgDados] = useState<{ ok: boolean; texto: string } | null>(null);
  const [atual, setAtual] = useState('');
  const [nova, setNova] = useState('');
  const [repete, setRepete] = useState('');
  const [msgSenha, setMsgSenha] = useState<{ ok: boolean; texto: string } | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const carregar = useCallback(async () => {
    const r = await chamarApi<DadosDaConta>('GET', '/api/participante/conta');
    if (!r.ok) { setErroCarga(r.erro); if (r.expirou) setSessao(null); return; }
    setDados(r.dados);
    setTelefone(r.dados.pessoa?.telefone ? mascaraTelefone(r.dados.pessoa.telefone) : '');
  }, []);

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (s) void carregar();
    const doEndereco = window.location.hash.slice(1) as Aba;
    if (ABAS.some(a => a.id === doEndereco)) setAba(doEndereco);
  }, [carregar]);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Minha conta</h1>
        <p className="muted">Entre na sua conta para ver os seus dados.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent('/minha-conta')}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  if (erroCarga) return <div className="card conta"><p className="aviso bad" role="alert">{erroCarga}</p></div>;
  if (!dados) return <div className="card conta centro"><p>Carregando…</p></div>;

  const p = dados.pessoa;

  async function salvarTelefone() {
    setMsgDados(null);
    if (!p) return;
    const digitos = telefone.replace(/\D/g, '');
    if (digitos.length < 10) return setMsgDados({ ok: false, texto: 'Informe o telefone com DDD.' });
    setOcupado(true);
    const r = await chamarApi('POST', '/api/participante/vinculo', { nome: p.nome, cpf: '', nascimento: p.nascimento, sexo: p.sexo, telefone: digitos });
    setOcupado(false);
    if (!r.ok) return setMsgDados({ ok: false, texto: r.erro });
    setMsgDados({ ok: true, texto: 'Telefone atualizado.' });
    void carregar();
  }

  async function trocarSenha() {
    setMsgSenha(null);
    if (!atual) return setMsgSenha({ ok: false, texto: 'Informe a senha atual.' });
    if (!senhaValida(nova)) return setMsgSenha({ ok: false, texto: 'A nova senha precisa de 8 caracteres ou mais, com letra e número.' });
    if (nova !== repete) return setMsgSenha({ ok: false, texto: 'As duas senhas novas não são iguais.' });
    setOcupado(true);
    const r = await chamarApi<Parameters<typeof iniciarSessao>[0]>('POST', '/api/identidade/trocar-senha', { senhaAtual: atual, novaSenha: nova });
    setOcupado(false);
    if (!r.ok) return setMsgSenha({ ok: false, texto: r.erro });
    iniciarSessao(r.dados);   // o servidor devolve um token novo
    setAtual(''); setNova(''); setRepete('');
    setMsgSenha({ ok: true, texto: 'Senha alterada.' });
  }

  return (
    <div className="caixa-conta">
      <h1>Minha conta</h1>
      <p className="muted">Seus dados, sua senha e as autorizações que você deu.</p>

      <div className="abas" role="tablist">
        {ABAS.map(a => (
          <button key={a.id} role="tab" aria-selected={aba === a.id} className={aba === a.id ? 'on' : ''} onClick={() => { setAba(a.id); setMsgDados(null); setMsgSenha(null); }}>{a.nome}</button>
        ))}
      </div>

      {aba === 'dados' && (
        <section className="card" aria-label="Dados pessoais">
          <h2>Dados pessoais</h2>
          {!p ? (
            <p className="muted">Você ainda não tem cadastro. Ele é criado quando você faz a primeira inscrição. <Link href="/#ins">Ver inscrições abertas</Link></p>
          ) : (
            <>
              <p className="aviso info">CPF e data de nascimento são verificados pela associação e só mudam por ela. Para corrigir, <Link href="/contato">fale com a diretoria</Link>.</p>
              <div className="f"><label htmlFor="mc-nome">Nome</label><input id="mc-nome" type="text" value={p.nome} readOnly /></div>
              <div className="cols">
                <div className="f"><label htmlFor="mc-cpf">CPF</label><input id="mc-cpf" type="text" value={p.cpfMascarado} readOnly /></div>
                <div className="f"><label htmlFor="mc-nasc">Nascimento</label><input id="mc-nasc" type="text" value={isoParaBr(p.nascimento)} readOnly /></div>
              </div>
              <div className="cols">
                <div className="f"><label htmlFor="mc-tel">Telefone / WhatsApp</label><input id="mc-tel" type="tel" inputMode="tel" value={telefone} onChange={e => setTelefone(mascaraTelefone(e.target.value))} /></div>
                <div className="f"><label htmlFor="mc-email">E-mail da conta</label><input id="mc-email" type="email" value={sessao.usuario.email} readOnly /></div>
              </div>
              {msgDados && <p className={`aviso ${msgDados.ok ? 'ok' : 'bad'}`} role="status">{msgDados.texto}</p>}
              <button className="btn primary" type="button" disabled={ocupado} onClick={salvarTelefone}>Salvar</button>
            </>
          )}
        </section>
      )}

      {aba === 'senha' && (
        <section className="card" aria-label="Alterar senha">
          <h2>Alterar senha</h2>
          <CampoSenha id="mc-atual" rotulo="Senha atual" valor={atual} aoMudar={setAtual} />
          <CampoSenha id="mc-nova" rotulo="Nova senha" valor={nova} aoMudar={setNova} nova />
          <CampoSenha id="mc-repete" rotulo="Repita a nova senha" valor={repete} aoMudar={setRepete} autoComplete="new-password" />
          {msgSenha && <p className={`aviso ${msgSenha.ok ? 'ok' : 'bad'}`} role="status">{msgSenha.texto}</p>}
          <button className="btn primary" type="button" disabled={ocupado} onClick={trocarSenha}>Alterar senha</button>
        </section>
      )}

      {aba === 'autorizacoes' && (
        <section className="card" aria-label="Autorizações">
          <h2>Autorizações</h2>
          {dados.autorizacoes.length === 0 ? (
            <p className="muted">Você ainda não deu nenhuma autorização: elas são pedidas a cada inscrição.</p>
          ) : (
            <div className="tabela-rolavel">
              <table>
                <thead><tr><th>Inscrição</th><th>Autorização</th><th>Quando</th><th>Termo</th></tr></thead>
                <tbody>
                  {dados.autorizacoes.flatMap((a, k) => {
                    const origem = <><b>{a.campeonato} · {a.ano}</b>{a.protocolo && <div className="small muted">#{a.protocolo}</div>}</>;
                    const termo = a.versao ? `v${a.versao}` : '—';
                    const linhas = [<tr key={`${k}d`}><td>{origem}</td><td>Tratamento de dados pessoais (LGPD)</td><td>{dataEHora(a.dados)}</td><td>{termo}</td></tr>];
                    if (a.imagem) linhas.push(<tr key={`${k}i`}><td>{origem}</td><td>Uso de imagem e dados para divulgação</td><td>{dataEHora(a.imagem)}</td><td>{termo}</td></tr>);
                    return linhas;
                  })}
                </tbody>
              </table>
            </div>
          )}
          <p className="small muted">Para revogar uma autorização, ou pedir cópia ou eliminação dos seus dados, fale com a diretoria: <Link href="/privacidade">política de privacidade</Link> e <Link href="/contato">contato</Link>.</p>
        </section>
      )}

      <p><button className="btn" type="button" onClick={() => sair()}>Sair da conta</button></p>
    </div>
  );
}

'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { brl } from '@/lib/detalhe';
import { type ConviteConsultado } from '@/lib/equipe';
import { POSICOES, dataCurta, mascaraTelefone } from '@/lib/inscricao';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

/**
 * O link do convite que o atleta recebeu por e-mail (P09): mostra do que se trata sem pedir login, leva a entrar/criar a conta do e-mail convidado,
 * completa a ficha, pede as duas autorizações (só o próprio atleta as dá) e confirma. Também dá para recusar, sem conta.
 */
export default function ConviteDoAtleta({ codigo, versaoTermos }: { codigo: string; versaoTermos: string }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [convite, setConvite] = useState<ConviteConsultado | null>(null);
  const [naoAchou, setNaoAchou] = useState(false);
  const [erroCarga, setErroCarga] = useState('');
  const [passo, setPasso] = useState<'conta' | 'dados' | 'autorizacoes' | 'confirmado' | 'recusado'>('conta');
  const [erro, setErro] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [protocolo, setProtocolo] = useState('');

  const [telefone, setTelefone] = useState('');
  const [altura, setAltura] = useState('');
  const [peso, setPeso] = useState('');
  const [posicao, setPosicao] = useState('');
  const [plano, setPlano] = useState('');
  const [aceiteDados, setAceiteDados] = useState(false);
  const [aceiteImagem, setAceiteImagem] = useState(false);

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (s) setPasso('dados');
    void fetch(`/api/convites/${encodeURIComponent(codigo)}`)
      .then(async r => {
        if (r.status === 404) return setNaoAchou(true);
        if (!r.ok) return setErroCarga('Não foi possível abrir o convite agora. Tente de novo em instantes.');
        setConvite((await r.json()) as ConviteConsultado);
      })
      .catch(() => setErroCarga('Sem conexão com o servidor. Confira a internet e tente de novo.'));
  }, [codigo]);

  if (naoAchou) {
    return (
      <div className="card conta centro">
        <h1>Convite não encontrado</h1>
        <p className="muted">Este link não vale mais: o convite pode ter sido recusado, removido ou substituído por um novo. Peça ao responsável da equipe para reenviar.</p>
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }
  if (erroCarga) return <div className="card conta"><p className="aviso bad" role="alert">{erroCarga}</p></div>;
  if (!convite || sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;

  const titulo = `${convite.campeonato} ${convite.ano}`;
  const voltar = encodeURIComponent(`/convites/${codigo}`);

  if (convite.situacao !== 'Enviado' && passo !== 'confirmado' && passo !== 'recusado') {
    const texto = {
      Confirmado: 'Este convite já foi confirmado. Você acompanha tudo em “Minhas inscrições”.',
      Recusou: 'Este convite foi recusado.',
      Removido: 'Este convite não vale mais.',
      Vencido: 'O prazo deste convite terminou. Peça ao responsável da equipe para reenviar.',
      EquipeRecusada: 'A inscrição desta equipe não foi aprovada, então o convite não vale mais.',
    }[convite.situacao];
    return (
      <div className="card conta centro">
        <h1>Convite para a equipe {convite.equipe}</h1>
        <p className="muted">{texto}</p>
        {convite.situacao === 'Confirmado' && <Link className="btn primary block" href="/minhas-inscricoes">Minhas inscrições</Link>}
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  function continuarDados() {
    setErro('');
    const a = altura.trim() === '' ? null : Number(altura);
    const p = peso.trim() === '' ? null : Number(peso);
    if (telefone.replace(/\D/g, '').length < 10) return setErro('Informe o telefone com DDD.');
    if (a !== null && (!Number.isInteger(a) || a < 100 || a > 260)) return setErro('A altura deve estar entre 100 e 260 cm.');
    if (p !== null && (!Number.isInteger(p) || p < 30 || p > 250)) return setErro('O peso deve estar entre 30 e 250 kg.');
    setPasso('autorizacoes');
  }

  async function confirmar() {
    setErro('');
    if (!aceiteDados || !aceiteImagem) return setErro('Marque as duas autorizações para continuar.');
    setOcupado(true);
    const r = await chamarApi<{ protocolo: string }>('POST', `/api/participante/convites/${encodeURIComponent(codigo)}/confirmar`, {
      telefone: telefone.trim(),
      alturaCm: altura.trim() === '' ? null : Number(altura),
      pesoKg: peso.trim() === '' ? null : Number(peso),
      posicao: posicao || null,
      possuiPlanoSaude: plano.trim() !== '',
      nomePlanoSaude: plano.trim() || null,
      aceiteDados, aceiteImagem, versaoTermos,
    });
    setOcupado(false);
    if (!r.ok) { if (r.expirou) setSessao(null); return setErro(r.erro); }
    setProtocolo(r.dados.protocolo);
    setPasso('confirmado');
  }

  async function recusar() {
    if (!window.confirm('Não participar da equipe? O responsável será avisado e o seu convite deixa de valer.')) return;
    setOcupado(true);
    const r = await fetch(`/api/convites/${encodeURIComponent(codigo)}/recusar`, { method: 'POST' }).catch(() => null);
    setOcupado(false);
    if (!r?.ok) return setErro('Não foi possível recusar agora. Tente de novo.');
    setPasso('recusado');
  }

  if (passo === 'confirmado') {
    return (
      <div className="card conta centro">
        <div className="icone" aria-hidden="true">✅</div>
        <h1>Participação confirmada!</h1>
        <div className="proto" data-testid="protocolo-convite">Protocolo #{protocolo}</div>
        <p className="muted">O responsável foi avisado. Você acompanha tudo em “Minhas inscrições”.</p>
        <Link className="btn primary block" href="/minhas-inscricoes">Minhas inscrições</Link>
      </div>
    );
  }
  if (passo === 'recusado') {
    return (
      <div className="card conta centro">
        <h1>Convite recusado</h1>
        <p className="muted">Avisamos o responsável para que ele possa convidar outra pessoa. Nenhum dado seu foi mantido além do necessário para este aviso.</p>
        <Link className="btn block" href="/">Voltar ao início</Link>
      </div>
    );
  }

  return (
    <div className="card conta">
      <p className="aviso info"><b>{convite.primeiroNome}</b>, você foi incluído na equipe <b>{convite.equipe}</b> ({titulo}{convite.categoria ? `, ${convite.categoria}` : ''}) por <b>{convite.responsavel}</b>. Convite válido até {dataCurta(convite.validoAte)}.</p>
      {erro && <p className="aviso bad" role="alert">{erro}</p>}

      {(!sessao || passo === 'conta') && (
        <>
          <h2>1. Sua conta</h2>
          <p className="muted">O convite foi enviado para <b>{convite.emailMascarado}</b>. Entre ou crie a sua conta com esse e-mail para continuar.</p>
          <div className="acoes">
            <Link className="btn primary" href={`/entrar?aba=criar&voltar=${voltar}`}>Criar conta</Link>
            <Link className="btn" href={`/entrar?aba=entrar&voltar=${voltar}`}>Já tenho conta</Link>
          </div>
          <p style={{ marginTop: 16 }}><button className="btn sm" onClick={() => void recusar()} disabled={ocupado}>Não vou participar</button></p>
        </>
      )}

      {sessao && passo === 'dados' && (
        <>
          <h2>2. Confirme seus dados</h2>
          <p className="small muted">Nome, CPF e nascimento foram informados pelo responsável e ficam como estão. Se algo estiver errado, fale com ele ou com a diretoria antes de confirmar.</p>
          <div className="f"><label htmlFor="c-tel">Telefone / WhatsApp</label><input id="c-tel" type="text" inputMode="tel" value={telefone} onChange={e => setTelefone(mascaraTelefone(e.target.value))} /></div>
          <div className="g2">
            <div className="f"><label htmlFor="c-alt">Altura (cm)</label><input id="c-alt" type="text" inputMode="numeric" value={altura} onChange={e => setAltura(e.target.value.replace(/\D/g, '').slice(0, 3))} /></div>
            <div className="f"><label htmlFor="c-peso">Peso (kg)</label><input id="c-peso" type="text" inputMode="numeric" value={peso} onChange={e => setPeso(e.target.value.replace(/\D/g, '').slice(0, 3))} /></div>
          </div>
          <div className="f"><label htmlFor="c-pos">Posição</label>
            <select id="c-pos" value={posicao} onChange={e => setPosicao(e.target.value)}><option value="">Não informar</option>{POSICOES.map(p => <option key={p}>{p}</option>)}</select></div>
          <div className="f"><label htmlFor="c-plano">Plano de saúde</label><input id="c-plano" type="text" maxLength={100} placeholder="Nome do plano (deixe vazio se não tiver)" value={plano} onChange={e => setPlano(e.target.value)} /></div>
          <div className="acoes"><button className="btn" onClick={() => void recusar()} disabled={ocupado}>Não vou participar</button><button className="btn primary" onClick={continuarDados}>Continuar</button></div>
        </>
      )}

      {sessao && passo === 'autorizacoes' && (
        <>
          <h2>3. Autorizações</h2>
          <p className="muted">Você mesmo autoriza; ninguém pode fazer isso por você. Ficam registradas com data, hora e a versão do texto ({versaoTermos}).</p>
          <label className="cat"><input type="checkbox" id="c-dados" checked={aceiteDados} onChange={e => setAceiteDados(e.target.checked)} />
            <span><b>Tratamento dos meus dados pessoais (LGPD)</b><small>Autorizo a associação a tratar os dados informados para a inscrição, a organização do torneio, súmulas e estatísticas. <Link href="/privacidade" target="_blank">Política de privacidade</Link></small></span></label>
          <label className="cat"><input type="checkbox" id="c-imagem" checked={aceiteImagem} onChange={e => setAceiteImagem(e.target.checked)} />
            <span><b>Uso da minha imagem e dados para divulgação</b><small>Autorizo a divulgar meu nome, fotos, vídeos, resultados e estatísticas do torneio no site e nas redes da associação. <Link href="/termos" target="_blank">Termos de uso</Link></small></span></label>
          <p className="aviso warn">Torneio aberto: participar <b>não torna você associado</b>.</p>
          <p className="aviso info">{convite.taxa != null ? <>A taxa de inscrição é de <b>{brl(convite.taxa)} por atleta</b>. </> : 'A taxa de inscrição é por atleta. '}Nada é cobrado agora: a diretoria combina o pagamento com você ou com o responsável da equipe, e você entra no elenco quando a taxa for registrada.</p>
          <div className="acoes"><button className="btn" onClick={() => setPasso('dados')}>Voltar</button><button className="btn primary" id="c-confirmar" onClick={() => void confirmar()} disabled={ocupado}>{ocupado ? 'Confirmando...' : 'Confirmar participação'}</button></div>
        </>
      )}
    </div>
  );
}

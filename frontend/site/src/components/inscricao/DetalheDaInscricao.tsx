'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { ROTULO_FICHA, ROTULO_PEDIDO, dataCurta } from '@/lib/inscricao';
import { type DetalheDaInscricao as Dados, brl, dataEHora } from '@/lib/detalhe';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

type Pedido = Dados['categorias'][number];

/**
 * O acompanhamento de um pedido, em linguagem simples: três passos (pedido enviado, aprovado pela diretoria, inscrição confirmada)
 * e uma frase dizendo o que acontece agora e o que a pessoa precisa fazer, se precisar.
 */
function Acompanhamento({ c, enviadaEm, taxa }: { c: Pedido; enviadaEm: string; taxa: number | null }) {
  const recusada = c.status === 'Recusada';
  const etapa = c.status === 'Pendente' ? 1 : c.status === 'AguardandoPagamento' ? 2 : c.status === 'Efetivada' ? 3 : 1;
  const passos = [
    { n: 1, titulo: 'Pedido enviado', detalhe: dataCurta(enviadaEm) },
    { n: 2, titulo: recusada ? 'Pedido recusado' : 'Aprovado pela diretoria', detalhe: recusada && c.recusadaEm ? dataCurta(c.recusadaEm) : '' },
    { n: 3, titulo: 'Inscrição confirmada', detalhe: '' },
  ];
  const estado = (n: number) => (recusada ? (n === 1 ? 'feito' : n === 2 ? 'recusado' : 'espera') : n <= etapa ? 'feito' : n === etapa + 1 ? 'agora' : 'espera');

  return (
    <>
      <ol className="passos" aria-label="Etapas do pedido">
        {passos.map(p => (
          <li key={p.n} className={estado(p.n)} aria-current={estado(p.n) === 'agora' ? 'step' : undefined}>
            <span className="bola" aria-hidden="true">{estado(p.n) === 'feito' ? '✓' : estado(p.n) === 'recusado' ? '✕' : p.n}</span>
            <b>{p.titulo}</b>
            {p.detalhe && <small>{p.detalhe}</small>}
          </li>
        ))}
      </ol>
      <p className="o-que-agora"><b>O que acontece agora: </b>
        {c.status === 'Pendente' && 'a diretoria vai analisar o seu pedido. Você recebe um e-mail quando houver resposta; não precisa fazer nada.'}
        {c.status === 'AguardandoPagamento' && <>seu pedido foi aprovado. Para confirmar a inscrição falta pagar a taxa{taxa != null ? ` de ${brl(taxa)}` : ''}: <Link href="/contato">fale com a diretoria</Link> para combinar o pagamento. Quando ela confirmar que recebeu, a inscrição fica confirmada.</>}
        {c.status === 'Efetivada' && (c.equipe ? `sua inscrição está confirmada e você está na equipe ${c.equipe}.` : 'sua inscrição está confirmada. Em breve a diretoria coloca você em uma equipe.')}
        {recusada && <>seu pedido não foi aceito. <b>Motivo:</b> {c.motivoRecusa ?? 'não informado'}. Você pode fazer uma nova inscrição, escolhendo outra categoria.</>}
      </p>
    </>
  );
}

/** O detalhe de uma inscrição da conta (I7): linha do tempo por categoria, taxa e saldo (só leitura) e os dados enviados. */
export default function DetalheDaInscricao({ id }: { id: string }) {
  const [sessao, setSessao] = useState<SessaoDoSite | null | undefined>(undefined);
  const [d, setD] = useState<Dados | null>(null);
  const [erro, setErro] = useState('');
  const [naoAchou, setNaoAchou] = useState(false);

  useEffect(() => {
    const s = lerSessao();
    setSessao(s);
    if (!s) return;
    void chamarApi<Dados>('GET', `/api/participante/inscricoes/${id}`).then(r => {
      if (r.ok) setD(r.dados);
      else if (r.status === 404) setNaoAchou(true);
      else { setErro(r.erro); if (r.expirou) setSessao(null); }
    });
  }, [id]);

  if (sessao === undefined) return <div className="card conta centro"><p>Carregando…</p></div>;
  if (!sessao) {
    return (
      <div className="card conta centro">
        <h1>Inscrição</h1>
        <p className="muted">Entre na sua conta para ver esta inscrição.</p>
        <Link className="btn primary block" href={`/entrar?voltar=${encodeURIComponent(`/minhas-inscricoes/${id}`)}`}>Entrar ou criar conta</Link>
      </div>
    );
  }
  if (naoAchou) {
    return (
      <div className="card conta centro">
        <h1>Inscrição não encontrada</h1>
        <p className="muted">Ela não existe ou não é da sua conta.</p>
        <Link className="btn block" href="/minhas-inscricoes">Ver minhas inscrições</Link>
      </div>
    );
  }
  if (erro) return <div className="card conta"><p className="aviso bad" role="alert">{erro}</p></div>;
  if (!d) return <div className="card conta centro"><p>Carregando…</p></div>;

  const f = d.financeiro;
  const nova = d.categorias.some(c => c.status === 'Recusada');
  const pedidoAprovado = d.categorias.find(c => c.status === 'AguardandoPagamento');
  const efetivadas = d.categorias.filter(c => c.status === 'Efetivada');

  return (
    <>
      <p className="small muted"><Link href="/minhas-inscricoes">Minhas inscrições</Link> › {d.protocolo ? `#${d.protocolo}` : 'inscrição'}</p>
      <div className="linha entre">
        <div>
          <h1>{d.campeonato} · {d.ano}</h1>
          <p className="muted">{d.protocolo ? <>protocolo <b>#{d.protocolo}</b> · </> : null}enviada em <b>{dataCurta(d.enviadaEm)}</b></p>
        </div>
        <span className={`badge st-${d.situacao}`} data-testid="situacao">{ROTULO_FICHA[d.situacao]}</span>
      </div>

      {pedidoAprovado && (
        <p className="aviso warn" role="status">
          <b>Sua inscrição em {pedidoAprovado.nome} foi aprovada.</b>{' '}
          {f?.taxa != null && !f.taxaPaga ? `Falta pagar a taxa de ${brl(f.taxa)} para efetivar. ` : ''}
          Fale com a diretoria para combinar o pagamento: <Link href="/contato">Contato</Link>.
        </p>
      )}
      {efetivadas.length > 0 && (
        <p className="aviso ok" role="status">
          <b>Tudo certo!</b> Você está inscrito em {efetivadas.map(c => c.equipe ? `${c.nome} (equipe ${c.equipe})` : c.nome).join(', ')}.
        </p>
      )}
      {d.situacao === 'Recusada' && (
        <p className="aviso bad" role="status"><b>Seu pedido foi recusado.</b> Você pode fazer uma nova inscrição, escolhendo outra categoria.</p>
      )}
      {nova && <p><Link className="btn primary sm" href={`/inscricao/${d.temporadaId}`}>Nova inscrição</Link></p>}

      <div className="grid-detalhe">
        <div>
          {d.categorias.map(c => (
            <div className="card" key={c.id} data-categoria={c.nome}>
              <div className="linha entre" style={{ marginBottom: 14 }}>
                <h3 style={{ margin: 0 }}>{c.nome}</h3>
                <span className={`badge st-${c.status}`}>{ROTULO_PEDIDO[c.status]}</span>
              </div>
              <Acompanhamento c={c} enviadaEm={d.enviadaEm} taxa={f?.taxa ?? null} />
            </div>
          ))}
        </div>
        <div>
          {f && f.algumPedidoAprovado && (
          <div className="card" data-testid="pagamento">
            <h3>Pagamento</h3>
            {(
              <>
                {f.taxa != null ? (
                  <div className="dinheiro">
                    <span>Taxa de inscrição</span><b>{brl(f.taxa)}</b>
                    <span>Pago</span><b className={f.pago ? 'pago' : ''}>{brl(f.pago)}</b>
                    <span className="tot">{f.taxaPaga ? 'Taxa quitada' : 'Taxa a pagar'}</span><span className="tot">{brl(f.taxaPaga ? 0 : Math.max(0, f.taxa - f.pago))}</span>
                  </div>
                ) : <p className="muted">O valor da taxa ainda será definido pela associação.</p>}
                {f.cobrancaGerada && f.valorFinal != null && (
                  <div className="dinheiro" style={{ marginTop: 12 }}>
                    <span>Total das categorias</span><b>{brl(f.valorTotal ?? 0)}</b>
                    {(f.desconto ?? 0) > 0 && <><span>Desconto</span><b className="pago">− {brl(f.desconto ?? 0)}</b></>}
                    <span className="tot">Saldo em aberto</span><span className="tot">{brl(Math.max(0, f.saldo ?? 0))}</span>
                  </div>
                )}
                <p className="small muted" style={{ margin: '12px 0 0' }}>
                  {f.cobrancaGerada
                    ? 'Pagamento online em breve. Por enquanto, a associação confirma os pagamentos por aqui.'
                    : f.algumPedidoAprovado && !f.taxaPaga
                      ? 'Pagamento online em breve. Por enquanto, a associação envia as instruções e confirma o pagamento por aqui.'
                      : 'O valor das categorias é definido após o encerramento das inscrições. Você será avisado.'}
                </p>
              </>
            )}
          </div>
          )}
          <div className="card">
            <h3>Dados desta inscrição</h3>
            <dl className="kv small">
              <dt>Altura / peso</dt><dd>{d.ficha.alturaCm ? `${d.ficha.alturaCm} cm` : '—'} / {d.ficha.pesoKg ? `${d.ficha.pesoKg} kg` : '—'}</dd>
              <dt>Posição</dt><dd>{d.ficha.posicao ?? '—'}</dd>
              <dt>Plano de saúde</dt><dd>{d.ficha.possuiPlanoSaude ? d.ficha.nomePlanoSaude : 'Não'}</dd>
              {d.origem === 'Site' && d.ficha.autorizouImagem && (
                <><dt>Autorizações</dt><dd>Dados e imagem · termos v{d.ficha.versaoTermos} · {dataEHora(d.ficha.autorizouDados)}</dd></>
              )}
            </dl>
          </div>
        </div>
      </div>
    </>
  );
}

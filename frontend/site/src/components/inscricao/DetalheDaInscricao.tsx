'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { type StatusDoPedido, ROTULO_FICHA, ROTULO_PEDIDO, dataCurta } from '@/lib/inscricao';
import { type DetalheDaInscricao as Dados, brl, dataEHora } from '@/lib/detalhe';
import { chamarApi, lerSessao, type SessaoDoSite } from '@/lib/sessao';

type Pedido = Dados['categorias'][number];

/** A linha do tempo de um pedido: derivada da situação dele. */
function LinhaDoTempo({ c, enviadaEm }: { c: Pedido; enviadaEm: string }) {
  if (c.status === 'Recusada') {
    return (
      <ul className="tl">
        <li className="done"><b>Pedido enviado</b><span className="small muted">{dataCurta(enviadaEm)}</span></li>
        <li className="bad"><b>Recusada</b><span className="small">{c.motivoRecusa ?? 'Sem motivo informado.'}</span></li>
      </ul>
    );
  }
  const ordem: StatusDoPedido[] = ['Pendente', 'AguardandoPagamento', 'Efetivada'];
  const atual = ordem.indexOf(c.status);
  const passos = [
    { t: 'Pedido enviado', sub: dataCurta(enviadaEm) },
    { t: atual === 0 ? 'Em análise pela diretoria' : 'Aprovada pela diretoria', sub: 'Falta pagar a taxa para efetivar' },
    { t: c.equipe ? `Efetivada — equipe ${c.equipe}` : 'Efetivada', sub: 'Você entra na equipe' },
  ];
  return (
    <ul className="tl">
      {passos.map((p, k) => {
        const cls = k === 0 || k < atual || (k === atual && atual === 2) ? 'done' : k === atual ? 'now' : 'wait';
        const sub = k === 0 ? p.sub : (cls === 'wait' || cls === 'now') ? p.sub : '';
        return <li key={k} className={cls}><b>{p.t}</b>{sub && <span className="small muted">{sub}</span>}</li>;
      })}
    </ul>
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
              <LinhaDoTempo c={c} enviadaEm={d.enviadaEm} />
            </div>
          ))}
        </div>
        <div>
          <div className="card" data-testid="pagamento">
            <h3>Pagamento</h3>
            {!f ? <p className="muted">Nenhum valor foi cobrado.</p> : (
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

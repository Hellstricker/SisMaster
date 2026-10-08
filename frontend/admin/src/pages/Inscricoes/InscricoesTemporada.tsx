import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  type CategoriaPedida,
  type Ficha,
  type StatusInscricao,
  aprovarInscricao,
  listarFichasDaTemporada,
  recusarInscricao,
  registrarPagamentoSaldo,
  registrarPagamentoTaxa,
} from '../../api/inscricoes';
import {
  type TemporadaDetalhe,
  alterarInscricoesHabilitadas,
  definirDesconto,
  definirTaxaInscricao,
  removerDesconto,
  definirValorCategoria,
  gerarCobrancas,
  obterTemporada,
} from '../../api/temporadas';
import { formatarData, formatarMoeda } from '../../utils/formatacao';
import ModalNovaInscricao from './ModalNovaInscricao';
import {
  ModalAprovar,
  ModalCobranca,
  ModalPagamento,
  ModalRecusar,
  ModalValoresCategorias,
} from './ModaisDiretoria';
import styles from './Inscricoes.module.css';

const STATUS: StatusInscricao[] = ['Pendente', 'AguardandoPagamento', 'Efetivada', 'Recusada'];
const ROTULO: Record<StatusInscricao, string> = {
  Pendente: 'Pendente',
  AguardandoPagamento: 'Aguardando pagamento',
  Efetivada: 'Efetivada',
  Recusada: 'Recusada',
};
const ESTILO: Record<StatusInscricao, string> = {
  Pendente: styles.stPendente,
  AguardandoPagamento: styles.stAguardandoPagamento,
  Efetivada: styles.stEfetivada,
  Recusada: styles.stRecusada,
};

type Ordem = 'nome' | 'idade' | 'envio' | 'situacao' | 'saldo';
const TAMANHOS = [10, 25, 50, 100];
const PESO_STATUS: Record<StatusInscricao, number> = { Pendente: 0, AguardandoPagamento: 1, Efetivada: 2, Recusada: 3 };

type Modal =
  | { tipo: 'nova' }
  | { tipo: 'aprovar'; ficha: Ficha; pedido: CategoriaPedida }
  | { tipo: 'recusar'; ficha: Ficha; pedido: CategoriaPedida }
  | { tipo: 'taxa'; ficha: Ficha }
  | { tipo: 'saldo'; ficha: Ficha }
  | { tipo: 'valores' }
  | { tipo: 'cobranca' };

/**
 * Inscrições da temporada: uma linha por ficha (pessoa), com as categorias pedidas dentro.
 * Aprovar/recusar são por categoria; taxa e saldo são por ficha.
 */
export default function InscricoesTemporada() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();

  const [temporada, setTemporada] = useState<TemporadaDetalhe | null>(null);
  const [fichas, setFichas] = useState<Ficha[]>([]);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [filtro, setFiltro] = useState<StatusInscricao | 'Todas'>('Todas');
  const [busca, setBusca] = useState('');
  const [ordem, setOrdem] = useState<Ordem>('nome');
  const [asc, setAsc] = useState(true);
  const [tamanho, setTamanho] = useState(25);
  const [pagina, setPagina] = useState(1);
  const [modal, setModal] = useState<Modal | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; erro: boolean } | null>(null);

  const categoriaFiltro = params.get('categoria') ?? 'todas';

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const [t, f] = await Promise.all([obterTemporada(id), listarFichasDaTemporada(id)]);
      setTemporada(t);
      setFichas(f);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar as inscrições.');
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

  function escolherCategoria(valor: string) {
    const p = new URLSearchParams(params);
    if (valor === 'todas') p.delete('categoria'); else p.set('categoria', valor);
    setParams(p, { replace: true });
  }

  const todosPedidos = useMemo(() => fichas.flatMap(f => f.categorias), [fichas]);

  const contagem = useMemo(() => {
    const c: Record<string, number> = { Todas: todosPedidos.length };
    STATUS.forEach(s => { c[s] = todosPedidos.filter(p => p.status === s).length; });
    return c;
  }, [todosPedidos]);

  const casa = useCallback(
    (p: CategoriaPedida) =>
      (filtro === 'Todas' || p.status === filtro) &&
      (categoriaFiltro === 'todas' || p.temporadaCategoriaId === categoriaFiltro),
    [filtro, categoriaFiltro],
  );

  const linhas = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    const digitos = busca.replace(/\D/g, '');
    return fichas.filter(f =>
      f.categorias.some(casa) &&
      (termo === '' || f.pessoa.nome.toLowerCase().includes(termo) || (digitos !== '' && f.pessoa.cpf.replace(/\D/g, '').includes(digitos))));
  }, [fichas, casa, busca]);

  const ordenadas = useMemo(() => {
    const dir = asc ? 1 : -1;
    const situacao = (f: Ficha) => Math.min(...f.categorias.filter(casa).map(c => PESO_STATUS[c.status]));
    const chave = (f: Ficha): string | number => {
      switch (ordem) {
        case 'idade': return f.pessoa.idade;
        case 'envio': return f.dataEnvio;
        case 'situacao': return situacao(f);
        case 'saldo': return f.financeiro.cobrancaGerada ? (f.financeiro.saldo ?? 0) : Number.NEGATIVE_INFINITY;
        default: return f.pessoa.nome;
      }
    };
    return [...linhas].sort((x, y) => {
      const a = chave(x), b = chave(y);
      const c = typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), 'pt-BR', { sensitivity: 'base' });
      return (c || x.pessoa.nome.localeCompare(y.pessoa.nome, 'pt-BR', { sensitivity: 'base' })) * (c ? dir : 1);
    });
  }, [linhas, ordem, asc, casa]);

  const totalPaginas = Math.max(1, Math.ceil(ordenadas.length / tamanho));
  const paginaAtual = Math.min(pagina, totalPaginas);
  const visiveis = ordenadas.slice((paginaAtual - 1) * tamanho, paginaAtual * tamanho);

  // Qualquer mudança de filtro, busca, ordem ou tamanho volta para a primeira página.
  useEffect(() => { setPagina(1); }, [filtro, categoriaFiltro, busca, ordem, asc, tamanho]);

  function ordenarPor(o: Ordem) {
    if (ordem === o) setAsc(a => !a); else { setOrdem(o); setAsc(true); }
  }
  const seta = (o: Ordem) => (ordem === o ? (asc ? ' ▲' : ' ▼') : '');

  /** Executa a ação, fecha o modal, avisa e recarrega; erros sobem para o modal que chamou. */
  async function executar(fn: () => Promise<void>, sucesso: string) {
    await fn();
    setModal(null);
    setAviso({ texto: sucesso, erro: false });
    await carregar();
  }

  async function acaoDireta(fn: () => Promise<void>, sucesso: string) {
    try { await executar(fn, sucesso); } catch (e) {
      setAviso({ texto: e instanceof Error ? e.message : 'Operação não concluída.', erro: true });
    }
  }

  if (loading) return <div className={styles.page}><p className={styles.info}>Carregando...</p></div>;
  if (erro || !temporada) {
    return <div className={styles.page}><p className={styles.erroGlobal}>{erro || 'Temporada não encontrada.'}</p></div>;
  }

  const habilitadas = temporada.inscricoesHabilitadas;
  const encerrada = temporada.status === 'Encerrada';
  const taxa = temporada.taxaInscricao;

  return (
    <div className={styles.page}>
      <div className={styles.topbar}>
        <button className={styles.btnVoltar} onClick={() => navigate(`/temporadas/${temporada.id}`)}>‹ Voltar</button>
      </div>

      <h1 className={styles.titulo}>Inscrições — Temporada {temporada.ano}</h1>
      <p className={styles.sub}>
        A pessoa se inscreve na temporada, em uma ou mais categorias, numa única ficha. Cada categoria é aprovada ou recusada
        separadamente; a taxa e a cobrança final pertencem à ficha.
      </p>

      <div className={styles.card}>
        <div className={styles.chips}>
          <span className={styles.chip}>Taxa de inscrição (fixa, 1× por ficha): <b>{taxa == null ? 'a definir' : formatarMoeda(taxa)}</b></span>
          <span className={styles.chip}>
            Desconto:{' '}
            <b>
              {temporada.descontos.length === 0
                ? 'nenhum'
                : temporada.descontos.map(d => `${d.categorias.join(' + ')} −${d.tipo === 'Percentual' ? `${d.valor}%` : formatarMoeda(d.valor)}`).join(' · ')}
            </b>
          </span>
          {temporada.categorias.map(c => (
            <span key={c.id} className={styles.chip}>
              {c.nome} · {c.idadeMinima > 0 ? `${c.idadeMinima}+` : 'qualquer idade'} · {c.sexo ?? 'mista'}
              {c.aceitaAbaixoIdadeMinima ? ' · aceita abaixo da idade' : ''} · <b>{c.valor == null ? 'a definir' : formatarMoeda(c.valor)}</b>
            </span>
          ))}
        </div>
        <p className={styles.dica}>O valor de cada categoria é definido depois do fim das inscrições; quem joga mais de uma pode receber desconto conforme a combinação de categorias.</p>
      </div>

      <div className={`${styles.banner} ${habilitadas ? styles.bannerOn : styles.bannerOff}`}>
        <span>
          {habilitadas
            ? <><b>Inscrições habilitadas.</b> Novas fichas estão sendo aceitas.</>
            : <><b>Inscrições desabilitadas.</b> Nenhuma nova ficha é aceita{encerrada ? '' : '; já é possível gerar as cobranças finais'}.</>}
        </span>
        <span className={styles.bannerAcoes}>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'cobranca' })} disabled={encerrada}>Taxa e descontos</button>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'valores' })} disabled={encerrada}>Valores das categorias</button>
          <button
            className={`${styles.btnSecondary} ${styles.btnSm}`}
            disabled={habilitadas || encerrada}
            title={habilitadas ? 'Desabilite as inscrições antes de gerar as cobranças' : 'Calcula o valor final de todas as fichas da temporada'}
            onClick={() => acaoDireta(() => gerarCobrancas(temporada.id), 'Cobranças geradas.')}
          >
            Gerar cobranças
          </button>
          <button
            className={`${styles.btnSecondary} ${styles.btnSm}`}
            disabled={encerrada}
            onClick={() => acaoDireta(
              () => alterarInscricoesHabilitadas(temporada.campeonatoId, temporada.id, !habilitadas),
              habilitadas ? 'Inscrições desabilitadas.' : 'Inscrições habilitadas.')}
          >
            {habilitadas ? 'Desabilitar inscrições' : 'Habilitar inscrições'}
          </button>
        </span>
      </div>

      <div className={styles.toolbar}>
        <select className={`${styles.select} ${styles.filtroCat}`} value={categoriaFiltro} onChange={e => escolherCategoria(e.target.value)} aria-label="Filtrar por categoria">
          <option value="todas">Todas as categorias</option>
          {temporada.categorias.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
        </select>
        <input className={`${styles.input} ${styles.busca}`} placeholder="Buscar por nome ou CPF" value={busca} onChange={e => setBusca(e.target.value)} />
        <button className={`${styles.btnPrimary} ${styles.btnNova}`} disabled={!habilitadas} onClick={() => setModal({ tipo: 'nova' })} title={habilitadas ? '' : 'Inscrições desabilitadas'}>
          + Nova inscrição
        </button>
      </div>

      <div className={styles.toolbar}>
        <div className={styles.tabs}>
          {(['Todas', ...STATUS] as const).map(s => (
            <button key={s} className={`${styles.tab} ${filtro === s ? styles.tabAtiva : ''}`} onClick={() => setFiltro(s)}>
              {s === 'Todas' ? 'Todas' : ROTULO[s]}<small>{contagem[s]}</small>
            </button>
          ))}
        </div>
      </div>

      <div className={styles.barraTabela}>
        <span className={styles.mutado}>
          {ordenadas.length === 0 ? 'Nenhuma ficha' : `Mostrando ${(paginaAtual - 1) * tamanho + 1}–${Math.min(paginaAtual * tamanho, ordenadas.length)} de ${ordenadas.length} fichas`}
        </span>
        <span className={styles.paginacaoControles}>
          <label className={styles.mutado}>
            Por página{' '}
            <select className={styles.select} value={tamanho} onChange={e => setTamanho(Number(e.target.value))} aria-label="Fichas por página">
              {TAMANHOS.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </label>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={paginaAtual <= 1} onClick={() => setPagina(1)} aria-label="Primeira página">«</button>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={paginaAtual <= 1} onClick={() => setPagina(paginaAtual - 1)}>‹ Anterior</button>
          <span>Página {paginaAtual} de {totalPaginas}</span>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={paginaAtual >= totalPaginas} onClick={() => setPagina(paginaAtual + 1)}>Próxima ›</button>
          <button className={`${styles.btnSecondary} ${styles.btnSm}`} disabled={paginaAtual >= totalPaginas} onClick={() => setPagina(totalPaginas)} aria-label="Última página">»</button>
        </span>
      </div>

      <div className={styles.tabelaWrap}>
        <table className={styles.tabela}>
          <thead>
            <tr>
              <th>
                <button className={styles.thOrdem} onClick={() => ordenarPor('nome')}>Pessoa{seta('nome')}</button>
                {' · '}
                <button className={styles.thOrdem} onClick={() => ordenarPor('idade')}>Idade{seta('idade')}</button>
              </th>
              <th>Ficha</th>
              <th><button className={styles.thOrdem} onClick={() => ordenarPor('envio')}>Enviada em{seta('envio')}</button></th>
              <th><button className={styles.thOrdem} onClick={() => ordenarPor('situacao')}>Categorias pedidas{seta('situacao')}</button></th>
              <th><button className={styles.thOrdem} onClick={() => ordenarPor('saldo')}>Financeiro (da ficha){seta('saldo')}</button></th>
              <th className={styles.direita}>Ficha</th>
            </tr>
          </thead>
          <tbody>
            {linhas.length === 0 && <tr><td colSpan={6} className={styles.vazio}>Nenhuma inscrição neste filtro.</td></tr>}
            {visiveis.map(f => {
              const fin = f.financeiro;
              const todasRecusadas = f.categorias.every(c => c.status === 'Recusada');
              const temAguardando = f.categorias.some(c => c.status === 'AguardandoPagamento');
              const temEfetivada = f.categorias.some(c => c.status === 'Efetivada');
              const saldoAberto = fin.cobrancaGerada && (fin.saldo ?? 0) > 0;
              return (
                <tr key={f.id}>
                  <td className={styles.pessoa}>
                    <b>{f.pessoa.nome}</b>
                    <span>{f.pessoa.cpf} · {f.pessoa.idade} anos · {f.pessoa.perfil}</span>
                  </td>
                  <td className={styles.mutado}>
                    {f.dados.alturaCm ? `${f.dados.alturaCm} cm` : '—'} · {f.dados.pesoKg ? `${f.dados.pesoKg} kg` : '—'} · {f.dados.posicao ?? '—'}
                  </td>
                  <td>{formatarData(f.dataEnvio)}</td>
                  <td>
                    {f.categorias.map(c => (
                      <div key={c.id} className={`${styles.catLinha} ${casa(c) ? '' : styles.catLinhaOff}`}>
                        <span className={styles.catNome}>{c.nome}</span>
                        <span className={`${styles.selo} ${ESTILO[c.status]}`}>{ROTULO[c.status]}</span>
                        {c.foraDoEsperado.length > 0 && !c.justificativaExcecao && c.status === 'Pendente' &&
                          <span className={styles.tagExc} title={c.foraDoEsperado.join('; ')}>fora do esperado</span>}
                        {c.justificativaExcecao && <span className={styles.tagExc} title={c.justificativaExcecao}>exceção aprovada</span>}
                        {c.status === 'Pendente' && (
                          <span className={styles.catAcoes}>
                            <button className={`${styles.btnPrimary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'aprovar', ficha: f, pedido: c })}>Aprovar</button>
                            <button className={`${styles.btnSecondary} ${styles.btnSm} ${styles.btnPerigo}`} onClick={() => setModal({ tipo: 'recusar', ficha: f, pedido: c })}>Recusar</button>
                          </span>
                        )}
                        {c.recusadaEm && <div className={styles.catInfo}>em {formatarData(c.recusadaEm)}: {c.motivoRecusa}</div>}
                        {c.justificativaExcecao && <div className={styles.catInfo}>{c.justificativaExcecao}</div>}
                      </div>
                    ))}
                  </td>
                  <td>
                    {todasRecusadas ? <span className={styles.mutado}>—</span> : (
                      <>
                        <span className={`${styles.selo} ${fin.taxaPaga ? styles.stEfetivada : styles.stPendente}`}>
                          {fin.taxaPaga ? 'Taxa paga' : 'Taxa pendente'}
                        </span>
                        {fin.cobrancaGerada ? (
                          <div className={styles.mini}>
                            Final {formatarMoeda(fin.valorFinal ?? 0)} (desc. {formatarMoeda(fin.valorDesconto ?? 0)})
                            <br /><b>Saldo {formatarMoeda(fin.saldo ?? 0)}</b>
                          </div>
                        ) : <div className={styles.mini}>Cobrança final: não gerada</div>}
                      </>
                    )}
                  </td>
                  <td>
                    <div className={styles.acoes}>
                      {temAguardando && (
                        <button className={`${styles.btnPrimary} ${styles.btnSm}`} disabled={taxa == null}
                          title={taxa == null ? 'Defina a taxa de inscrição da temporada' : ''}
                          onClick={() => setModal({ tipo: 'taxa', ficha: f })}>
                          Registrar taxa{taxa != null ? ` (${formatarMoeda(taxa)})` : ''}
                        </button>
                      )}
                      {temEfetivada && saldoAberto && (
                        <button className={`${styles.btnPrimary} ${styles.btnSm}`} onClick={() => setModal({ tipo: 'saldo', ficha: f })}>Registrar saldo</button>
                      )}
                      {!temAguardando && !(temEfetivada && saldoAberto) && (
                        todasRecusadas
                          ? <span className={styles.mutado}>Histórico</span>
                          : temEfetivada ? <span className={styles.mutado}>{fin.cobrancaGerada ? 'Quitada · ' : ''}pronta para equipe</span> : null
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {modal?.tipo === 'nova' && (
        <ModalNovaInscricao
          temporadaId={temporada.id}
          anoTemporada={temporada.ano}
          categorias={temporada.categorias}
          categoriasIniciais={categoriaFiltro === 'todas' ? [] : [categoriaFiltro]}
          onFechar={() => setModal(null)}
          onEnviado={() => { setModal(null); setAviso({ texto: 'Inscrição enviada.', erro: false }); carregar(); }}
        />
      )}
      {modal?.tipo === 'aprovar' && (
        <ModalAprovar nome={`${modal.ficha.pessoa.nome} — ${modal.pedido.nome}`} foraDoEsperado={modal.pedido.foraDoEsperado} onFechar={() => setModal(null)}
          onConfirmar={just => executar(() => aprovarInscricao(modal.pedido.id, just), 'Inscrição aprovada.')} />
      )}
      {modal?.tipo === 'recusar' && (
        <ModalRecusar nome={`${modal.ficha.pessoa.nome} — ${modal.pedido.nome}`} onFechar={() => setModal(null)}
          onConfirmar={motivo => executar(() => recusarInscricao(modal.pedido.id, motivo), 'Categoria recusada e mantida no histórico.')} />
      )}
      {modal?.tipo === 'taxa' && (
        <ModalPagamento tipo="taxa" nome={modal.ficha.pessoa.nome} taxa={taxa} onFechar={() => setModal(null)}
          onConfirmar={({ dataIso }) => executar(() => registrarPagamentoTaxa(modal.ficha.id, dataIso),
            `Taxa registrada: ${modal.ficha.pessoa.nome} agora é Associado.`)} />
      )}
      {modal?.tipo === 'saldo' && (
        <ModalPagamento tipo="saldo" nome={modal.ficha.pessoa.nome} saldo={modal.ficha.financeiro.saldo ?? 0} onFechar={() => setModal(null)}
          onConfirmar={({ valor, dataIso }) => executar(() => registrarPagamentoSaldo(modal.ficha.id, valor!, dataIso), 'Pagamento registrado.')} />
      )}
      {modal?.tipo === 'valores' && (
        <ModalValoresCategorias
          categorias={temporada.categorias}
          onFechar={() => setModal(null)}
          onSalvar={async (categoriaId, valor) => {
            await definirValorCategoria(temporada.id, categoriaId, valor);
            setAviso({ texto: 'Valor salvo.', erro: false });
            await carregar();
          }}
        />
      )}
      {modal?.tipo === 'cobranca' && (
        <ModalCobranca
          taxaAtual={taxa}
          descontos={temporada.descontos}
          categorias={temporada.categorias}
          onFechar={() => setModal(null)}
          onSalvarTaxa={async v => { await definirTaxaInscricao(temporada.id, v); setAviso({ texto: 'Taxa salva.', erro: false }); await carregar(); }}
          onSalvarDesconto={async d => { await definirDesconto(temporada.id, d); setAviso({ texto: 'Desconto salvo.', erro: false }); await carregar(); }}
          onRemoverDesconto={async d => { await removerDesconto(temporada.id, d.id); setAviso({ texto: 'Desconto removido.', erro: false }); await carregar(); }}
        />
      )}

      {aviso && <div className={`${styles.toast} ${aviso.erro ? styles.toastErro : ''}`} role="status">{aviso.texto}</div>}
    </div>
  );
}

import { useMemo, useState } from 'react';
import type { JogoLista, JogosDaTemporada, StatusJogo } from '../../api/jogos';
import DataInput, { dataValida } from '../../components/DataInput/DataInput';
import base from '../Fases/Fases.module.css';
import styles from './Jogos.module.css';
import { STATUS_ROTULO, horaCurta, rotuloDia, rotuloLocal } from './formatacaoDeJogos';
import { type OpcoesDaTabela, OPCOES_PADRAO, baixar, jogosDaTabela, montarCsv } from './tabelaDeJogos';

/** Painel "Imprimir / exportar": escolhe o que entra e gera a página para imprimir ou o CSV. */
export function ImprimirTabelaModal({ dados, onFechar, onAviso }: { dados: JogosDaTemporada; onFechar: () => void; onAviso: (texto: string) => void }) {
  const [o, setO] = useState<OpcoesDaTabela>(OPCOES_PADRAO);
  const [formato, setFormato] = useState<'papel' | 'csv'>('papel');
  const [papel, setPapel] = useState(false);
  const jogos = useMemo(() => jogosDaTabela(dados.jogos, o), [dados.jogos, o]);

  const locais = useMemo(() => {
    const mapa = new Map<string, string>();
    for (const j of dados.jogos) if (j.local) mapa.set(j.local.id, rotuloLocal(j.local));
    return [...mapa.entries()];
  }, [dados.jogos]);

  const periodoInvalido = (o.de !== '' && !dataValida(o.de, 2000, 2100)) || (o.ate !== '' && !dataValida(o.ate, 2000, 2100));

  function gerar() {
    if (formato === 'csv') {
      baixar(`tabela-jogos-${dados.ano}.csv`, montarCsv(jogos, o.placar));
      onAviso(`CSV gerado com ${jogos.length} jogo${jogos.length === 1 ? '' : 's'}.`);
      onFechar();
    } else {
      setPapel(true);
    }
  }

  if (papel) return <PapelDeImpressao dados={dados} jogos={jogos} opcoes={o} onFechar={onFechar} />;

  return (
    <div className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <div className={base.modal} style={{ maxWidth: 640 }}>
        <h2 className={base.modalTitulo}>Imprimir / exportar a tabela</h2>
        <div className={base.linha2}>
          <div className={base.field}>
            <label htmlFor="im-cat">Categoria</label>
            <select id="im-cat" className={base.select} value={o.categoria} onChange={e => setO({ ...o, categoria: e.target.value })}>
              <option value="">Todas</option>
              {dados.categorias.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
          <div className={base.field}>
            <label htmlFor="im-loc">Local</label>
            <select id="im-loc" className={base.select} value={o.local} onChange={e => setO({ ...o, local: e.target.value })}>
              <option value="">Todos</option>
              {locais.map(([id, nome]) => <option key={id} value={id}>{nome}</option>)}
            </select>
          </div>
        </div>
        <div className={base.linha2}>
          <div className={base.field}>
            <label htmlFor="im-st">Status do jogo</label>
            <select id="im-st" className={base.select} value={o.status} onChange={e => setO({ ...o, status: e.target.value })}>
              <option value="">Todos</option>
              {(Object.keys(STATUS_ROTULO) as StatusJogo[]).map(s => <option key={s} value={s}>{STATUS_ROTULO[s]}</option>)}
            </select>
          </div>
          <div className={base.field}>
            <label htmlFor="im-agr">Agrupar por</label>
            <select id="im-agr" className={base.select} value={o.agrupar} onChange={e => setO({ ...o, agrupar: e.target.value as 'data' | 'local' })}>
              <option value="data">Data</option>
              <option value="local">Local</option>
            </select>
          </div>
        </div>
        <div className={base.linha2}>
          <div className={base.field}><label htmlFor="im-de">De (data)</label><DataInput id="im-de" value={o.de} onChange={v => setO({ ...o, de: v })} anoMin={2000} anoMax={2100} /></div>
          <div className={base.field}><label htmlFor="im-ate">Até (data)</label><DataInput id="im-ate" value={o.ate} onChange={v => setO({ ...o, ate: v })} anoMin={2000} anoMax={2100} /></div>
        </div>
        <label className={styles.check}><input type="checkbox" checked={o.placar} onChange={e => setO({ ...o, placar: e.target.checked })} /> Mostrar o placar dos encerrados</label>
        <label className={styles.check}><input type="checkbox" checked={o.semData} onChange={e => setO({ ...o, semData: e.target.checked })} /> Incluir jogos sem data</label>

        <p className={base.dica} role="status">
          {periodoInvalido ? 'Confira o período: use datas válidas (dd/mm/aaaa).' : `${jogos.length} jogo${jogos.length === 1 ? '' : 's'} no resultado.`}
        </p>

        <label className={`${styles.formato} ${formato === 'papel' ? styles.formatoSel : ''}`}>
          <input type="radio" name="formato" checked={formato === 'papel'} onChange={() => setFormato('papel')} />
          <span><b>Página para imprimir</b><small>Cabeçalho com campeonato e temporada, jogos agrupados, sem botões. Imprime ou salva em PDF pelo navegador.</small></span>
        </label>
        <label className={`${styles.formato} ${formato === 'csv' ? styles.formatoSel : ''}`}>
          <input type="radio" name="formato" checked={formato === 'csv'} onChange={() => setFormato('csv')} />
          <span><b>CSV (planilha)</b><small>Nº, data, hora, local, categoria, fase, rodada, casa, visitante, status e placar. Sem dados pessoais.</small></span>
        </label>

        <div className={base.modalFooter}>
          <button type="button" className={base.btnSecondary} onClick={onFechar}>Cancelar</button>
          <button type="button" className={base.btnPrimary} onClick={gerar} disabled={jogos.length === 0 || periodoInvalido}>Gerar</button>
        </div>
      </div>
    </div>
  );
}

const CSS_DE_IMPRESSAO = `
#papel-impressao { background:#fff; color:#111; font-family:Georgia,serif; padding:22px 26px; border-radius:6px; max-width:900px; margin:0 auto; }
#papel-impressao h2 { margin:0; font-size:20px; }
#papel-impressao .pi-sub { color:#555; font-size:12px; margin:2px 0 14px; }
#papel-impressao h3 { font-size:14px; margin:14px 0 4px; border-bottom:2px solid #111; padding-bottom:2px; }
#papel-impressao h4 { font-size:12px; margin:8px 0 2px; color:#555; }
#papel-impressao table { width:100%; border-collapse:collapse; }
#papel-impressao td { border-top:1px solid #ddd; padding:4px 6px; font-size:12px; vertical-align:top; }
#papel-impressao td.pi-n { width:38px; color:#777; } #papel-impressao td.pi-h { width:52px; font-weight:700; }
#papel-impressao td.pi-c { width:90px; color:#555; }
#papel-impressao .pi-rodape { font-size:10px; color:#777; margin-top:14px; }
@media print {
  body * { visibility: hidden; }
  #papel-overlay, #papel-impressao, #papel-impressao * { visibility: visible; }
  #papel-overlay { position: absolute !important; left:0; top:0; width:100%; padding:0 !important; background:none !important; overflow:visible !important; }
  #papel-impressao { max-width:none; padding:0; border-radius:0; }
  .pi-nao-imprimir { display:none !important; }
  #papel-impressao h3 { break-after: avoid; } #papel-impressao tr { break-inside: avoid; }
}`;

function PapelDeImpressao({ dados, jogos, opcoes, onFechar }: { dados: JogosDaTemporada; jogos: JogoLista[]; opcoes: OpcoesDaTabela; onFechar: () => void }) {
  const [geradoEm] = useState(() => new Date().toLocaleDateString('pt-BR'));
  const categoria = dados.categorias.find(c => c.id === opcoes.categoria)?.nome;
  const subtitulo = ['Tabela de jogos', categoria ?? 'todas as categorias',
    opcoes.status ? `jogos ${STATUS_ROTULO[opcoes.status as StatusJogo].toLowerCase()}` : null].filter(Boolean).join(' · ');

  const grupos = useMemo(() => {
    const chave = (j: JogoLista) => (opcoes.agrupar === 'data' ? (j.data ? rotuloDia(j.data) : 'Sem data definida') : (j.local ? rotuloLocal(j.local) : 'Local a definir'));
    const mapa = new Map<string, JogoLista[]>();
    for (const j of jogos) mapa.set(chave(j), [...(mapa.get(chave(j)) ?? []), j]);
    return [...mapa.entries()];
  }, [jogos, opcoes.agrupar]);

  const sub = (j: JogoLista) => (opcoes.agrupar === 'data' ? (j.local ? rotuloLocal(j.local) : 'Local a definir') : (j.data ? rotuloDia(j.data) : 'Sem data'));

  return (
    <div id="papel-overlay" className={base.overlay} onMouseDown={e => { if (e.target === e.currentTarget) onFechar(); }}>
      <style>{CSS_DE_IMPRESSAO}</style>
      <div style={{ width: '100%', maxWidth: 980 }}>
        <div className="pi-nao-imprimir" style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginBottom: 10 }}>
          <button type="button" className={base.btnSecondary} onClick={onFechar}>Fechar</button>
          <button type="button" className={base.btnPrimary} onClick={() => window.print()}>Imprimir</button>
        </div>
        <div id="papel-impressao">
          <h2>{dados.campeonatoNome} — Temporada {dados.ano}</h2>
          <div className="pi-sub">{subtitulo}</div>
          {grupos.map(([titulo, doGrupo]) => (
            <section key={titulo}>
              <h3>{titulo}</h3>
              <table>
                <tbody>
                  {doGrupo.map((j, i) => (
                    <FragmentoDoPapel key={j.id} subtitulo={i === 0 || sub(doGrupo[i - 1]) !== sub(j) ? sub(j) : null}>
                      <tr>
                        <td className="pi-n">#{j.numero}</td>
                        <td className="pi-h">{horaCurta(j.hora) || '—'}</td>
                        <td>{j.casa.texto}{opcoes.placar && j.status === 'Encerrado' ? ` ${j.placarCasa}` : ''} × {opcoes.placar && j.status === 'Encerrado' ? `${j.placarVisitante} ` : ''}{j.visitante.texto}</td>
                        <td className="pi-c">{j.categoria.nome}</td>
                        <td>{j.fase.nome} · {j.etiqueta}</td>
                      </tr>
                    </FragmentoDoPapel>
                  ))}
                </tbody>
              </table>
            </section>
          ))}
          <div className="pi-rodape">Gerado em {geradoEm} · SisMaster</div>
        </div>
      </div>
    </div>
  );
}

function FragmentoDoPapel({ subtitulo, children }: { subtitulo: string | null; children: React.ReactNode }) {
  return (
    <>
      {subtitulo && <tr><td colSpan={5}><h4>{subtitulo}</h4></td></tr>}
      {children}
    </>
  );
}

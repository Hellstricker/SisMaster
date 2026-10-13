import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { type TextosLegaisApi, obterAssociacao, obterTextosLegais, publicarTextoLegal } from '../../api/associacoes';
import { formatarData } from '../../utils/formatacao';
import base from '../Fases/Fases.module.css';

const MARCADORES = ['{{nome}}', '{{sigla}}', '{{cnpj}}', '{{endereco}}', '{{encarregado}}', '{{emailPrivacidade}}', '{{foro}}', '{{emailContato}}'];

/**
 * Textos legais da associação (política de privacidade e termos de uso): cada alteração é uma versão nova, que nunca se edita nem se
 * apaga. Texto em branco = continua o texto padrão do sistema. Mudança relevante sobe o número principal e vale como novo aceite
 * nas próximas inscrições e cadastros.
 */
export default function TextosLegais() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [nome, setNome] = useState('');
  const [dados, setDados] = useState<TextosLegaisApi | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState('');
  const [aviso, setAviso] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [aberta, setAberta] = useState<string | null>(null);

  const [relevante, setRelevante] = useState(false);
  const [resumo, setResumo] = useState('');
  const [privacidade, setPrivacidade] = useState('');
  const [termos, setTermos] = useState('');

  const carregar = useCallback(async () => {
    if (!id) return;
    try {
      setErro('');
      const [a, t] = await Promise.all([obterAssociacao(id), obterTextosLegais(id)]);
      setNome(a.nome);
      setDados(t);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os textos.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { carregar(); }, [carregar]);

  useEffect(() => {
    if (!aviso) return;
    const t = setTimeout(() => setAviso(''), 5000);
    return () => clearTimeout(t);
  }, [aviso]);

  const vigente = dados?.versoes[0];

  function partirDaVigente() {
    setPrivacidade(vigente?.textoPrivacidade ?? '');
    setTermos(vigente?.textoTermos ?? '');
  }

  async function publicar() {
    if (!id) return;
    if (!resumo.trim()) return setErro('Descreva o que mudou nesta versão.');
    setSalvando(true);
    setErro('');
    try {
      const r = await publicarTextoLegal(id, { relevante, resumo: resumo.trim(), textoPrivacidade: privacidade, textoTermos: termos });
      setAviso(`Versão ${r.versao} publicada. Já está no site.`);
      setResumo(''); setPrivacidade(''); setTermos(''); setRelevante(false);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível publicar.');
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
      <h1 className={base.titulo}>Textos legais</h1>
      <p className={base.sub}>
        Política de privacidade e termos de uso do site. Versão em vigor: <b data-testid="vigente">{dados?.vigente ?? '1.0'}</b>
        {vigente ? ` (desde ${formatarData(vigente.vigenteDesde)})` : ' (texto padrão do sistema)'}.
      </p>

      {erro && <p className={base.erroGlobal} role="alert">{erro}</p>}

      <section className={base.card} style={{ marginBottom: 14 }}>
        <h2 className={base.modalTitulo}>Publicar nova versão</h2>
        <p className={base.dica} style={{ marginTop: 0 }}>
          A versão publicada nunca é editada: para mudar de novo, publique outra. Deixe um texto em branco para usar o texto padrão do sistema.
          Escrita em texto simples: <code>## Título</code>, <code>### Subtítulo</code>, <code>- item</code> para listas, linha em branco entre parágrafos e <code>**negrito**</code>.
          Os dados da associação entram sozinhos nestes marcadores: {MARCADORES.map(m => <code key={m} style={{ marginRight: 6 }}>{m}</code>)}
        </p>
        <div className={base.field}>
          <label htmlFor="tl-resumo">O que mudou <small>(aparece para quem aceitou a versão anterior)</small></label>
          <input id="tl-resumo" className={base.input} maxLength={300} value={resumo} onChange={e => { setResumo(e.target.value); setErro(''); }} />
        </div>
        <label style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12 }}>
          <input type="checkbox" checked={relevante} onChange={e => setRelevante(e.target.checked)} />
          <span>Mudança relevante <small>(sobe para a próxima versão principal, ex.: 2.0, e vale como novo aceite)</small></span>
        </label>
        <div className={base.field}>
          <label htmlFor="tl-priv">Política de privacidade</label>
          <textarea id="tl-priv" className={base.input} rows={10} maxLength={60000} value={privacidade} onChange={e => setPrivacidade(e.target.value)} />
        </div>
        <div className={base.field}>
          <label htmlFor="tl-termos">Termos de uso</label>
          <textarea id="tl-termos" className={base.input} rows={10} maxLength={60000} value={termos} onChange={e => setTermos(e.target.value)} />
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          {vigente && (vigente.textoPrivacidade || vigente.textoTermos) && (
            <button className={base.btnSecondary} onClick={partirDaVigente} type="button">Começar do texto vigente</button>
          )}
          <button className={base.btnPrimary} onClick={publicar} disabled={salvando} type="button">{salvando ? 'Publicando...' : 'Publicar nova versão'}</button>
        </div>
      </section>

      <section className={base.card}>
        <h2 className={base.modalTitulo}>Histórico de versões</h2>
        {!dados || dados.versoes.length === 0 ? (
          <p className={base.info}>Nenhuma versão publicada ainda: vale o texto padrão do sistema (versão 1.0).</p>
        ) : (
          <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
            {dados.versoes.map(v => (
              <li key={v.versao} style={{ borderTop: '1px solid var(--border, #ddd)', padding: '10px 0' }}>
                <b>{v.versao}</b> · {formatarData(v.vigenteDesde)}{v.relevante ? ' · mudança relevante' : ''} · por {v.publicadaPor}
                <div>{v.resumo}</div>
                <small>
                  Privacidade: {v.temTextoPrivacidade ? 'texto próprio' : 'texto padrão'} · Termos: {v.temTextoTermos ? 'texto próprio' : 'texto padrão'}
                  {(v.temTextoPrivacidade || v.temTextoTermos) && (
                    <> · <button className={base.btnSecondary} style={{ padding: '0 8px' }} type="button" onClick={() => setAberta(aberta === v.versao ? null : v.versao)}>
                      {aberta === v.versao ? 'Ocultar texto' : 'Ver texto'}</button></>
                  )}
                </small>
                {aberta === v.versao && (
                  <pre style={{ whiteSpace: 'pre-wrap', background: 'rgba(0,0,0,.04)', padding: 10, borderRadius: 8, maxHeight: 320, overflow: 'auto' }}>
                    {v.textoPrivacidade ? `POLÍTICA DE PRIVACIDADE\n\n${v.textoPrivacidade}\n\n` : ''}{v.textoTermos ? `TERMOS DE USO\n\n${v.textoTermos}` : ''}
                  </pre>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
      {aviso && <div className={base.toast} role="status">{aviso}</div>}
    </div>
  );
}

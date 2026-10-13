import type { ReactNode } from 'react';

/** Negrito simples: **assim**. Todo o resto é texto puro (nunca HTML), então nada do que a associação escreve vira código na página. */
function Inline({ texto }: { texto: string }): ReactNode {
  return texto.split(/(\*\*[^*]+\*\*)/g).map((parte, i) =>
    parte.startsWith('**') && parte.endsWith('**') && parte.length > 4 ? <b key={i}>{parte.slice(2, -2)}</b> : parte);
}

/**
 * Mostra o texto legal escrito pela associação, em texto simples: "## Título" e "### Subtítulo", "- item" para listas,
 * linha em branco separa parágrafos e **negrito**.
 */
export default function TextoLegal({ texto }: { texto: string }) {
  const blocos: ReactNode[] = [];
  let itens: string[] = [];
  let paragrafo: string[] = [];
  const fecharLista = () => { if (itens.length) blocos.push(<ul key={`u${blocos.length}`}>{itens.map((t, i) => <li key={i}><Inline texto={t} /></li>)}</ul>); itens = []; };
  const fecharParagrafo = () => { if (paragrafo.length) blocos.push(<p key={`p${blocos.length}`}><Inline texto={paragrafo.join(' ')} /></p>); paragrafo = []; };

  for (const bruta of texto.replace(/\r\n/g, '\n').split('\n')) {
    const linha = bruta.trim();
    if (!linha) { fecharLista(); fecharParagrafo(); continue; }
    if (linha.startsWith('### ')) { fecharLista(); fecharParagrafo(); blocos.push(<h3 key={`h${blocos.length}`}>{linha.slice(4)}</h3>); continue; }
    if (linha.startsWith('## ')) { fecharLista(); fecharParagrafo(); blocos.push(<h2 key={`h${blocos.length}`}>{linha.slice(3)}</h2>); continue; }
    if (linha.startsWith('- ')) { fecharParagrafo(); itens.push(linha.slice(2)); continue; }
    fecharLista();
    paragrafo.push(linha);
  }
  fecharLista();
  fecharParagrafo();
  return <>{blocos}</>;
}

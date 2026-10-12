import type { Metadata } from 'next';
import { linkInstagram, linkWhatsapp, obterPagina } from '@/lib/api';

export const metadata: Metadata = { title: 'Contato' };

export default async function Contato() {
  const p = await obterPagina();
  const c = p?.contato;
  const zap = c?.whatsapp ? linkWhatsapp(c.whatsapp) : null;
  const insta = c?.instagram ? linkInstagram(c.instagram) : null;
  const canais = [
    c?.whatsapp && { chave: 'whatsapp', icone: '💬', titulo: 'WhatsApp', texto: c.whatsapp, href: zap },
    c?.instagram && { chave: 'instagram', icone: '📷', titulo: 'Instagram', texto: c.instagram, href: insta },
    c?.email && { chave: 'email', icone: '✉️', titulo: 'E-mail', texto: c.email, href: `mailto:${c.email}` },
    c?.ondeJogamos && { chave: 'onde', icone: '📍', titulo: 'Onde jogamos', texto: c.ondeJogamos, href: null },
  ].filter(Boolean) as { chave: string; icone: string; titulo: string; texto: string; href: string | null }[];

  return (
    <main className="wrap">
      <h1>Contato</h1>
      <p className="sub">Fale com a diretoria.</p>
      {!p && <p className="aviso" role="status">Não foi possível carregar esta página agora. Tente de novo em instantes.</p>}
      {p && canais.length === 0 && <p className="muted">A associação ainda não publicou os canais de contato.</p>}
      {canais.length > 0 && (
        <div className="grid g2">
          {canais.map(k => {
            const conteudo = (
              <>
                <span className="ic" aria-hidden="true">{k.icone}</span>
                <span><b>{k.titulo}</b><span className="muted small">{k.texto}</span></span>
              </>
            );
            return k.href
              ? <a key={k.chave} className="card canal" href={k.href} {...(k.href.startsWith('http') ? { target: '_blank', rel: 'noopener noreferrer' } : {})}>{conteudo}</a>
              : <div key={k.chave} className="card canal">{conteudo}</div>;
          })}
        </div>
      )}
    </main>
  );
}

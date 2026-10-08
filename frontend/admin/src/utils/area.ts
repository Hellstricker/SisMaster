/** O mesmo app React atende duas áreas: a gestão (/admin) e a do árbitro (/arbitros). */
export const AREA_ARBITROS = window.location.pathname === '/arbitros' || window.location.pathname.startsWith('/arbitros/');

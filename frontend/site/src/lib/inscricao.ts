/** Tipos e regras de tela da inscrição pela conta (o servidor revalida tudo; isto só ajuda a pessoa a preencher certo). */

export interface PessoaDoSite {
  nome: string;
  cpfMascarado: string;
  /** AAAA-MM-DD */
  nascimento: string;
  sexo: 'Masculino' | 'Feminino';
  telefone: string | null;
  email?: string;
  menor?: boolean;
}

export interface FichaAnterior {
  alturaCm: number | null;
  pesoKg: number | null;
  posicao: string | null;
  possuiPlanoSaude: boolean;
  nomePlanoSaude: string | null;
}

export interface CategoriaDoFormulario {
  id: string;
  nome: string;
  idadeMinima: number;
  sexo: 'Masculino' | 'Feminino' | null;
  aceitaAbaixoIdadeMinima: boolean;
  /** Protocolo do pedido ativo da pessoa nesta categoria (já pedida). */
  jaPedida: string | null;
}

export interface Preparo {
  temporada: { id: string; campeonato: string; ano: number; abertas: boolean; dataInicioInscricoes: string; dataFimInscricoes: string };
  pessoa: PessoaDoSite | null;
  ultimaFicha: FichaAnterior | null;
  categorias: CategoriaDoFormulario[];
}

export interface ResultadoDoVinculo {
  situacao: 'Vinculada' | 'Bloqueada' | 'Menor';
  pessoa: PessoaDoSite | null;
}

export interface InscricaoEnviada {
  protocolo: string;
  enviadaEm: string;
  categorias: number;
}

// ---------- máscaras e conversões ----------

export const mascaraCpf = (v: string) =>
  v.replace(/\D/g, '').slice(0, 11).replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d{1,2})$/, '$1-$2');

export const mascaraData = (v: string) => {
  const d = v.replace(/\D/g, '').slice(0, 8);
  return d.length <= 2 ? d : d.length <= 4 ? `${d.slice(0, 2)}/${d.slice(2)}` : `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
};

export const mascaraTelefone = (v: string) => {
  const d = v.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 2) return d;
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`;
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
};

/** "10/05/1975" -> "1975-05-10" (ou null se a data não existe). */
export function dataParaIso(br: string): string | null {
  if (br.length !== 10) return null;
  const [d, m, a] = br.split('/').map(Number);
  const dt = new Date(a, m - 1, d);
  if (a < 1900 || dt.getFullYear() !== a || dt.getMonth() !== m - 1 || dt.getDate() !== d || dt > new Date()) return null;
  return `${a}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}

export const isoParaBr = (iso: string) => iso.split('-').reverse().join('/');

/** Confere os dígitos verificadores do CPF (a mesma regra do servidor). */
export function cpfValido(texto: string): boolean {
  const c = texto.replace(/\D/g, '');
  if (c.length !== 11 || /^(\d)\1+$/.test(c)) return false;
  const dv = (s: string, peso: number) => { let t = 0; for (let i = 0; i < s.length; i++) t += Number(s[i]) * (peso - i); const r = (t * 10) % 11; return r === 10 ? 0 : r; };
  return dv(c.slice(0, 9), 10) === Number(c[9]) && dv(c.slice(0, 10), 11) === Number(c[10]);
}

export function maiorDeIdade(nascimentoIso: string, hoje = new Date()): boolean {
  const [a, m, d] = nascimentoIso.split('-').map(Number);
  return new Date(a + 18, m - 1, d) <= hoje;
}

/** Motivos de a pessoa fugir do esperado para a categoria (idade = ano da temporada − ano de nascimento). Vazio = dentro do esperado. */
export function foraDoEsperado(c: CategoriaDoFormulario, nascimentoIso: string, sexo: string, anoDaTemporada: number): string[] {
  const motivos: string[] = [];
  const idade = anoDaTemporada - Number(nascimentoIso.slice(0, 4));
  if (!c.aceitaAbaixoIdadeMinima && idade < c.idadeMinima) motivos.push(`${idade} anos (mínimo ${c.idadeMinima})`);
  if (c.sexo && c.sexo !== sexo) motivos.push(`categoria ${c.sexo.toLowerCase()}`);
  return motivos;
}

export const POSICOES = ['Armador', 'Ala-armador', 'Ala', 'Ala-pivô', 'Pivô'];

// ---------- minhas inscrições ----------

export type SituacaoDaFicha = 'AguardandoPagamento' | 'EmAnalise' | 'Efetivada' | 'Recusada';
export type StatusDoPedido = 'Pendente' | 'AguardandoPagamento' | 'Efetivada' | 'Recusada';

export interface MinhaInscricao {
  id: string;
  protocolo: string | null;
  origem: string;
  enviadaEm: string;
  temporadaId: string;
  campeonato: string | null;
  ano: number | null;
  situacao: SituacaoDaFicha;
  categorias: { id: string; nome: string; status: StatusDoPedido }[];
}

export interface MinhasInscricoes {
  inscricoes: MinhaInscricao[];
  acaoNecessaria: boolean;
}

export const ROTULO_FICHA: Record<SituacaoDaFicha, string> = {
  AguardandoPagamento: 'Aguardando pagamento', EmAnalise: 'Em análise', Efetivada: 'Confirmada', Recusada: 'Recusada',
};

export const ROTULO_PEDIDO: Record<StatusDoPedido, string> = {
  Pendente: 'Em análise', AguardandoPagamento: 'Aprovada', Efetivada: 'Confirmada', Recusada: 'Recusada',
};

/** Data/hora ISO do servidor (hora da Bahia, sem fuso) -> "08/10/2026". */
export const dataCurta = (iso: string) => isoParaBr(iso.slice(0, 10));

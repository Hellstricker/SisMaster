import { enviar, obter } from './http';

export type Sexo = 'Masculino' | 'Feminino';

export interface Categoria {
  id: string;
  nome: string;
  /** Em quantas temporadas a categoria está vinculada (impede a exclusão). */
  uso: number;
  idadeMinima: number;
  sexo: Sexo | null;
  aceitaAbaixoIdadeMinima: boolean;
  minimoPeriodosEmQuadra: number;
  minimoPeriodosForaQuadra: number;
}

export type DadosCategoria = Omit<Categoria, 'id' | 'uso'>;

/** Categorias são um catálogo da associação (não mais global), ordenadas por idade mínima. */
export function obterCategoriasDaAssociacao(associacaoId: string): Promise<Categoria[]> {
  return obter<Categoria[]>(`/api/associacoes/${associacaoId}/categorias`, 'Erro ao carregar categorias');
}

export function criarCategoria(associacaoId: string, dados: DadosCategoria): Promise<void> {
  return enviar('POST', `/api/associacoes/${associacaoId}/categorias`, dados, 'Erro ao criar categoria');
}

export function editarCategoria(associacaoId: string, categoriaId: string, dados: DadosCategoria): Promise<void> {
  return enviar('PATCH', `/api/associacoes/${associacaoId}/categorias/${categoriaId}`, dados, 'Erro ao editar categoria');
}

export function excluirCategoria(associacaoId: string, categoriaId: string): Promise<void> {
  return enviar('DELETE', `/api/associacoes/${associacaoId}/categorias/${categoriaId}`, undefined, 'Erro ao excluir categoria');
}

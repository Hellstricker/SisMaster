import { BASE, enviarComResposta, lerErro } from './http';
import type { Perfil } from '../auth/sessao';

export type SituacaoUsuario = 'Ativo' | 'Desativado' | 'Bloqueado';

export interface UsuarioResumo {
  id: string;
  nome: string;
  email: string;
  perfil: Perfil | null;
  associacoes: string[];
  situacao: SituacaoUsuario;
  senhaTemporaria: boolean;
}

export interface DadosUsuario {
  nome: string;
  perfil: Perfil;
  associacoes: string[];
}

const RAIZ = '/api/identidade/usuarios';

export async function listarUsuarios(): Promise<UsuarioResumo[]> {
  const res = await fetch(`${BASE}${RAIZ}`);
  if (!res.ok) throw await lerErro(res, 'Não foi possível carregar os usuários.');
  return (await res.json()) as UsuarioResumo[];
}

export const criarUsuario = (dados: DadosUsuario & { email: string }) =>
  enviarComResposta<UsuarioResumo>('POST', RAIZ, dados, 'Não foi possível criar o usuário.');

export const editarUsuario = (id: string, dados: DadosUsuario) =>
  enviarComResposta<UsuarioResumo>('PUT', `${RAIZ}/${id}`, dados, 'Não foi possível salvar o usuário.');

export type AcaoUsuario = 'desativar' | 'reativar' | 'desbloquear' | 'redefinir-senha';

export const acaoNoUsuario = (id: string, acao: AcaoUsuario) =>
  enviarComResposta<UsuarioResumo>('POST', `${RAIZ}/${id}/${acao}`, undefined, 'Não foi possível concluir a ação.');

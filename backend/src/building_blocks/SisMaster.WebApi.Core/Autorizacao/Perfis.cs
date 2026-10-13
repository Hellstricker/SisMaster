using SisMaster.Core.Enums;

namespace SisMaster.WebApi.Core.Autorizacao;

/// <summary>Nomes dos papéis (iguais aos do enum <see cref="PerfilUsuario"/>) para usar em atributos.</summary>
public static class Perfis
{
    public const string Administrador = nameof(PerfilUsuario.Administrador);
    public const string AdministradorDeAssociacao = nameof(PerfilUsuario.AdministradorDeAssociacao);
    public const string Arbitro = nameof(PerfilUsuario.Arbitro);
    public const string Participante = nameof(PerfilUsuario.Participante);

    /// <summary>Os perfis da equipe (quem opera o sistema). O Participante, conta do público, não está aqui.</summary>
    public static readonly string[] Equipe = [Administrador, AdministradorDeAssociacao, Arbitro];
}

/// <summary>Políticas de autorização. Tudo exige usuário autenticado; estas restringem por perfil.</summary>
public static class Politicas
{
    /// <summary>Gestão do campeonato: Administrador e Administrador de associação.</summary>
    public const string Gestao = "Gestao";

    /// <summary>Área do árbitro (súmula e mesa): os três perfis.</summary>
    public const string Arbitragem = "Arbitragem";

    /// <summary>Só o Administrador (global).</summary>
    public const string SomenteAdministrador = "SomenteAdministrador";

    /// <summary>Conta do público (Participante): suas próprias inscrições. Rotas da equipe nunca usam esta política.</summary>
    public const string Participante = "Participante";
}

/// <summary>Tipos de claim que o token carrega além dos padrões.</summary>
public static class TiposDeClaim
{
    public const string Papel = "role";

    /// <summary>Id de uma associação a que o usuário está vinculado (uma claim por associação). O Administrador não precisa ter.</summary>
    public const string Associacao = "assoc";

    public const string Nome = "nome";

    /// <summary>Presente (valor "1") enquanto o usuário usa uma senha temporária e precisa trocá-la.</summary>
    public const string TrocarSenha = "trocar-senha";

    /// <summary>Presente (valor "1") numa conta desativada: não consegue entrar, mas nada é apagado.</summary>
    public const string Desativado = "desativado";

    /// <summary>Versão dos Termos de uso e da Política de privacidade aceita no cadastro, e quando (Unix, segundos).</summary>
    public const string TermosVersao = "termos-versao";
    public const string TermosEm = "termos-em";

    /// <summary>Quando a conta do público foi criada e quando foi pedido o último e-mail de confirmação (Unix, segundos).</summary>
    public const string CriadaEm = "criada-em";
    public const string ConfirmacaoEm = "confirmacao-em";

    /// <summary>Quando (segundos Unix) a pessoa pediu a última recuperação de senha; limita a um pedido a cada poucos minutos.</summary>
    public const string RecuperacaoEm = "recuperacao-em";
}

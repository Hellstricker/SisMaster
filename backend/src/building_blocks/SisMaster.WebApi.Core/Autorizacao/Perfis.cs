using SisMaster.Core.Enums;

namespace SisMaster.WebApi.Core.Autorizacao;

/// <summary>Nomes dos papéis (iguais aos do enum <see cref="PerfilUsuario"/>) para usar em atributos.</summary>
public static class Perfis
{
    public const string Administrador = nameof(PerfilUsuario.Administrador);
    public const string AdministradorDeAssociacao = nameof(PerfilUsuario.AdministradorDeAssociacao);
    public const string Arbitro = nameof(PerfilUsuario.Arbitro);
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

    /// <summary>Quando (segundos Unix) a pessoa pediu a última recuperação de senha; limita a um pedido a cada poucos minutos.</summary>
    public const string RecuperacaoEm = "recuperacao-em";
}

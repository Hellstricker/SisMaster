namespace SisMaster.Core.Enums
{
    /// <summary>
    /// Perfis de acesso. O nome do enum é o nome do papel (role) gravado no token.
    /// Administrador: tudo, em todas as associações. AdministradorDeAssociacao: gestão só das associações vinculadas
    /// e criação dos árbitros dela. Arbitro: área do árbitro (súmula, mesa de tempo e de estatísticas) das associações vinculadas.
    /// </summary>
    public enum PerfilUsuario
    {
        Administrador = 1,
        AdministradorDeAssociacao = 2,
        Arbitro = 3
    }
}

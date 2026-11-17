namespace SisMaster.WebApps.WebApi.Domain.Associacao;

/// <summary>
/// A quem a temporada se dirige. <b>Associados</b>: ter uma inscrição efetivada (taxa paga) faz da pessoa associada naquela temporada.
/// <b>Aberto</b>: qualquer pessoa pode participar (por equipe ou individualmente) e participar nunca cria vínculo de associado.
/// </summary>
public enum TipoDePublico
{
    Associados = 0,
    Aberto = 1
}

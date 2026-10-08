using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Fases;

/// <summary>
/// Estrutura e regra de classificação de uma fase (tudo que a tabela de jogos precisa saber ao ser montada).
/// O tipo define o que vale: o que não pertence ao tipo é descartado em <see cref="Normalizar"/>.
/// </summary>
/// <param name="NumeroTurnos">Pontos corridos e grupos: quantas vezes cada dupla se enfrenta (1 a 3).</param>
/// <param name="NumeroGrupos">Grupos: de 2 a 8, com ao menos 2 equipes em cada.</param>
/// <param name="Distribuicao">Grupos: como as equipes entram em cada grupo.</param>
/// <param name="JogosPorConfronto">Mata-mata: melhor de 1, 3, 5 ou 7.</param>
/// <param name="NumeroConfrontos">Mata-mata: quantos confrontos a fase tem (1 a 16).</param>
/// <param name="ClassificadosPrimeiros">Quantos se classificam (por grupo, em Grupos; no total, em Pontos corridos). Nulo = todos.</param>
/// <param name="MelhoresExtras">Só em Grupos: quantos melhores (N+1)º colocados também se classificam.</param>
public sealed record EstruturaFase(
    int? NumeroTurnos,
    int? NumeroGrupos,
    DistribuicaoEquipes? Distribuicao,
    int? JogosPorConfronto,
    int? NumeroConfrontos,
    int? ClassificadosPrimeiros,
    int MelhoresExtras)
{
    public static readonly int[] JogosPorConfrontoPermitidos = [1, 3, 5, 7];

    /// <summary>Descarta o que não pertence ao tipo e valida o que pertence. <paramref name="equipes"/> = equipes da categoria (0 = ainda desconhecido).</summary>
    public static EstruturaFase Normalizar(TipoFase tipo, EstruturaFase e, int equipes)
    {
        switch (tipo)
        {
            case TipoFase.MataMata:
                if (e.JogosPorConfronto is null || !JogosPorConfrontoPermitidos.Contains(e.JogosPorConfronto.Value))
                    throw new DomainException("Mata-mata deve ser melhor de 1, 3, 5 ou 7 jogos");
                if (e.NumeroConfrontos is null || e.NumeroConfrontos < 1 || e.NumeroConfrontos > 16)
                    throw new DomainException("O número de confrontos deve estar entre 1 e 16");
                return new EstruturaFase(null, null, null, e.JogosPorConfronto, e.NumeroConfrontos, null, 0);

            case TipoFase.PontosCorridos:
                ValidarTurnos(e.NumeroTurnos);
                ValidarClassificados(e.ClassificadosPrimeiros, equipes);
                return new EstruturaFase(e.NumeroTurnos, null, null, null, null, e.ClassificadosPrimeiros, 0);

            case TipoFase.Grupos:
            {
                ValidarTurnos(e.NumeroTurnos);
                if (e.NumeroGrupos is null || e.NumeroGrupos < 2 || e.NumeroGrupos > 8)
                    throw new DomainException("O número de grupos deve estar entre 2 e 8");
                if (e.Distribuicao is null)
                    throw new DomainException("Informe como as equipes são distribuídas nos grupos");

                var porGrupo = equipes > 0 ? equipes / e.NumeroGrupos.Value : 0;
                if (equipes > 0 && porGrupo < 2)
                    throw new DomainException($"{equipes} equipes não formam {e.NumeroGrupos} grupos de pelo menos 2 equipes");

                ValidarClassificados(e.ClassificadosPrimeiros, porGrupo);
                if (e.MelhoresExtras < 0)
                    throw new DomainException("Quantidade de melhores colocados inválida");
                if (e.MelhoresExtras > 0)
                {
                    if (e.ClassificadosPrimeiros is null)
                        throw new DomainException("Melhores colocados extras só valem quando se classificam apenas os primeiros de cada grupo");
                    if (equipes > 0 && e.ClassificadosPrimeiros >= porGrupo)
                        throw new DomainException($"Não existe o {e.ClassificadosPrimeiros + 1}º colocado em cada grupo para completar com os melhores");
                    if (e.MelhoresExtras >= e.NumeroGrupos)
                        throw new DomainException("Os melhores colocados extras devem ser menos que o número de grupos");
                }
                return new EstruturaFase(e.NumeroTurnos, e.NumeroGrupos, e.Distribuicao, null, null, e.ClassificadosPrimeiros, e.MelhoresExtras);
            }

            default:
                throw new DomainException("Tipo de fase inválido");
        }
    }

    private static void ValidarTurnos(int? turnos)
    {
        if (turnos is null || turnos < 1 || turnos > 3)
            throw new DomainException("O número de turnos deve estar entre 1 e 3");
    }

    private static void ValidarClassificados(int? n, int maximo)
    {
        if (n is null) return;
        if (n < 1 || (maximo > 0 && n > maximo))
            throw new DomainException(maximo > 0
                ? $"Quantos se classificam deve estar entre 1 e {maximo}"
                : "Quantos se classificam deve ser ao menos 1");
    }
}

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SisMaster.WebApi.Core.Usuarios;

namespace SisMaster.WebApps.WebApi.Seguranca;

/// <summary>
/// Barra o acesso a recursos de uma associação à qual o usuário não está vinculado (o Administrador acessa todas).
/// Olha, antes da ação: os parâmetros de rota com nome próprio (temporadaId, jogoId...), o <c>id</c> (conforme <see cref="IdEhAttribute"/>)
/// e as propriedades dos comandos do corpo (TemporadaId, JogoId...). Se o recurso não existe, deixa seguir (a ação responde 404).
/// Rotas [AllowAnonymous] não passam por aqui.
/// </summary>
public sealed class EscopoDeAssociacaoFilter(IAcessoDoUsuario acesso, IResolvedorDeAssociacao resolvedor) : IAsyncActionFilter
{
    private static readonly Dictionary<string, Recurso> PorNome = new(StringComparer.OrdinalIgnoreCase)
    {
        ["associacaoId"] = Recurso.Associacao,
        ["campeonatoId"] = Recurso.Campeonato,
        ["temporadaId"] = Recurso.Temporada,
        ["temporadaCategoriaId"] = Recurso.TemporadaCategoria,
        ["categoriaId"] = Recurso.Categoria,
        ["inscricaoId"] = Recurso.Inscricao,
        ["inscricaoCategoriaId"] = Recurso.InscricaoCategoria,
        ["equipeId"] = Recurso.Equipe,
        ["faseId"] = Recurso.Fase,
        ["jogoId"] = Recurso.Jogo,
        ["sumulaId"] = Recurso.Sumula,
        ["partidaId"] = Recurso.Sumula,
        ["localId"] = Recurso.Local,
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate next)
    {
        var publica = contexto.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (publica || !acesso.Autenticado || acesso.EhAdministrador)
        {
            await next();
            return;
        }

        foreach (var (recurso, id) in ColetarAlvos(contexto))
        {
            var associacao = await resolvedor.AssociacaoDoAsync(recurso, id, contexto.HttpContext.RequestAborted);
            if (associacao is not null && !acesso.PodeAcessar(associacao.Value))
            {
                contexto.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Você não tem acesso a esta associação."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }
        }

        await next();
    }

    private static IEnumerable<(Recurso, Guid)> ColetarAlvos(ActionExecutingContext contexto)
    {
        var alvos = new HashSet<(Recurso, Guid)>();

        // 1) parâmetros de rota: com nome próprio, e "id" conforme o atributo (da ação, ou do controller)
        foreach (var (chave, valor) in contexto.RouteData.Values)
        {
            if (!Guid.TryParse(valor?.ToString(), out var guid)) continue;
            if (chave.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                var attr = contexto.ActionDescriptor.EndpointMetadata.OfType<IdEhAttribute>().LastOrDefault();
                if (attr is not null) alvos.Add((attr.Recurso, guid));
            }
            else if (PorNome.TryGetValue(chave, out var r)) alvos.Add((r, guid));
        }

        // 2) propriedades dos comandos/corpos
        foreach (var argumento in contexto.ActionArguments.Values)
        {
            if (argumento is null || argumento is string || argumento.GetType().IsPrimitive || argumento is Guid) continue;
            foreach (var p in argumento.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?))
                {
                    if (PorNome.TryGetValue(p.Name, out var r) && p.GetValue(argumento) is Guid g && g != Guid.Empty) alvos.Add((r, g));
                }
                else if (typeof(IEnumerable<Guid>).IsAssignableFrom(p.PropertyType) && p.Name.EndsWith("Ids", StringComparison.Ordinal))
                {
                    // listas de ids (JogoIds, TemporadaCategoriaIds...): cada item é conferido
                    if (PorNome.TryGetValue(p.Name[..^1], out var r) && p.GetValue(argumento) is IEnumerable<Guid> lista)
                        foreach (var g in lista.Where(g => g != Guid.Empty)) alvos.Add((r, g));
                }
            }
        }

        return alvos;
    }
}

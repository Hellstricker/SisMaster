using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api")]
public class FasesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IFaseRepository _repository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly DetalheFaseQuery _detalheQuery;
    private readonly ClassificacaoFaseQuery _classificacaoQuery;

    public FasesController(IMediatorHandler mediator, IFaseRepository repository, IEquipeRepository equipeRepository,
        ITemporadaRepository temporadaRepository, IJogoRepository jogoRepository, DetalheFaseQuery detalheQuery, ClassificacaoFaseQuery classificacaoQuery)
    {
        _mediator = mediator;
        _repository = repository;
        _equipeRepository = equipeRepository;
        _temporadaRepository = temporadaRepository;
        _jogoRepository = jogoRepository;
        _detalheQuery = detalheQuery;
        _classificacaoQuery = classificacaoQuery;
    }

    /// <summary>Detalhe da fase (Tela 8): estrutura, vagas/grupos, confrontos e jogos (ou a prévia, antes de gerar a tabela).</summary>
    [HttpGet("fases/{id:guid}")]
    public async Task<ActionResult> Detalhar(Guid id, CancellationToken ct)
    {
        var detalhe = await _detalheQuery.ExecutarAsync(id, ct);
        return detalhe is null ? NotFound() : CustomResponse(detalhe);
    }

    /// <summary>Classificação da fase (Tela 12), calculada dos jogos encerrados: tabela, grupos ou séries do mata-mata.</summary>
    [HttpGet("fases/{id:guid}/classificacao")]
    public async Task<ActionResult> Classificar(Guid id, CancellationToken ct)
    {
        var resultado = await _classificacaoQuery.ExecutarAsync(id, ct);
        return resultado is null ? NotFound() : CustomResponse(resultado);
    }

    /// <summary>Distribuição manual: o grupo de cada vaga da fase (na ordem das vagas), até a tabela de jogos ser gerada.</summary>
    [HttpPut("fases/{id:guid}/distribuicao-manual")]
    public async Task<ActionResult> DefinirDistribuicaoManual(Guid id, [FromBody] DefinirDistribuicaoManualCommand command)
    {
        command.FaseId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Registra o sorteio da diretoria para um empate sem critério na classificação (fase inteira ou grupo).</summary>
    [HttpPut("fases/{id:guid}/sorteio")]
    public async Task<ActionResult> DefinirSorteio(Guid id, [FromBody] DefinirSorteioCommand command)
    {
        command.FaseId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Cria ou substitui o confronto <paramref name="numero"/> da fase de mata-mata (cruzamento entre duas origens).</summary>
    [HttpPut("fases/{id:guid}/confrontos/{numero:int}")]
    public async Task<ActionResult> DefinirConfronto(Guid id, int numero, [FromBody] DefinirConfrontoCommand command)
    {
        command.FaseId = id;
        command.Numero = numero;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Tudo que a tela precisa: estado do cadastro de fases e, por categoria, a sequência de fases.</summary>
    [HttpGet("temporadas/{temporadaId:guid}/fases")]
    public async Task<ActionResult> ListarDaTemporada(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await _temporadaRepository.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return NotFound();

        var fases = await _repository.ListarPorTemporadaAsync(temporadaId, ct);
        var equipes = await _equipeRepository.ContarPorCategoriaAsync(temporadaId, ct);
        var statusDosJogos = await _jogoRepository.StatusPorFaseAsync(temporadaId, ct);

        var statusPermite = temporada.Status is StatusTemporada.InscricoesEncerradas or StatusTemporada.EmAndamento;
        return CustomResponse(new
        {
            temporada.Id,
            temporada.Ano,
            temporada.Status,
            PermiteCadastrar = statusPermite && !temporada.CadastroFasesEncerrado,
            CadastroEncerrado = temporada.CadastroFasesEncerrado,
            temporada.CadastroFasesEncerradoEm,
            temporada.TabelaJogosGerada,
            PodeReabrir = statusPermite && temporada.CadastroFasesEncerrado && !temporada.TabelaJogosGerada,
            Categorias = temporada.Categorias.OrderBy(c => c.Nome).Select(c => new
            {
                TemporadaCategoriaId = c.Id,
                c.Nome,
                Equipes = equipes.GetValueOrDefault(c.Id),
                Fases = fases.Where(f => f.TemporadaCategoriaId == c.Id).Select(f => new
                {
                    f.Id,
                    f.Ordem,
                    f.Nome,
                    f.Tipo,
                    Status = StatusFaseDerivado.De(statusDosJogos.GetValueOrDefault(f.Id) ?? []),
                    f.NumeroTurnos,
                    f.NumeroGrupos,
                    f.Distribuicao,
                    f.JogosPorConfronto,
                    f.NumeroConfrontos,
                    f.ClassificadosPrimeiros,
                    f.MelhoresExtras,
                    f.EstruturaCompleta,
                    f.FaseAnteriorId,
                    FaseAnteriorNome = fases.FirstOrDefault(x => x.Id == f.FaseAnteriorId)?.Nome,
                    // Outra fase vem desta: não pode ser excluída enquanto isso valer.
                    TemDependente = fases.Any(x => x.FaseAnteriorId == f.Id)
                })
            })
        });
    }

    [HttpPost("temporadas/{temporadaId:guid}/fases")]
    public async Task<ActionResult> Criar(Guid temporadaId, [FromBody] CriarFaseCommand command)
    {
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("fases/{id:guid}")]
    public async Task<ActionResult> Editar(Guid id, [FromBody] EditarFaseCommand command)
    {
        command.FaseId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("fases/{id:guid}")]
    public async Task<ActionResult> Excluir(Guid id)
    {
        var result = await _mediator.EnviarComando(new ExcluirFaseCommand { FaseId = id });
        return CustomResponse(result);
    }

    [HttpPatch("fases/{id:guid}/mover")]
    public async Task<ActionResult> Mover(Guid id, [FromBody] MoverFaseCommand command)
    {
        command.FaseId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("temporadas/{temporadaId:guid}/fases/encerrar-cadastro")]
    public async Task<ActionResult> EncerrarCadastro(Guid temporadaId)
    {
        var result = await _mediator.EnviarComando(new EncerrarCadastroFasesCommand { TemporadaId = temporadaId });
        return CustomResponse(result);
    }

    [HttpPost("temporadas/{temporadaId:guid}/fases/reabrir-cadastro")]
    public async Task<ActionResult> ReabrirCadastro(Guid temporadaId)
    {
        var result = await _mediator.EnviarComando(new ReabrirCadastroFasesCommand { TemporadaId = temporadaId });
        return CustomResponse(result);
    }
}

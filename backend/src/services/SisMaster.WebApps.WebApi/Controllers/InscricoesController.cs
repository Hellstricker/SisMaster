using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api")]
public class InscricoesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IInscricaoRepository _repository;
    private readonly ITemporadaRepository _temporadaRepository;

    public InscricoesController(IMediatorHandler mediator, IInscricaoRepository repository, ITemporadaRepository temporadaRepository)
    {
        _mediator = mediator;
        _repository = repository;
        _temporadaRepository = temporadaRepository;
    }

    private static object ProjetarFinanceiro(Inscricao i) => new
    {
        i.TaxaPaga,
        i.TotalPago,
        i.CobrancaGerada,
        i.ValorTotal,
        i.ValorDesconto,
        i.ValorFinal,
        i.Saldo,
        Pagamentos = i.Pagamentos.OrderBy(p => p.Data).Select(p => new { p.Id, p.Tipo, p.Valor, Data = p.Data.ToString("yyyy-MM-dd") })
    };

    private static object ProjetarPessoa(Pessoa p, int anoTemporada) => new
    {
        p.Id,
        p.Nome,
        Cpf = p.Cpf.ToString(),
        Nascimento = p.Nascimento.ToString("yyyy-MM-dd"),
        Idade = anoTemporada - p.Nascimento.Year,
        p.Sexo,
        p.Perfil
    };

    /// <summary>Fichas da temporada: uma por envio, com os pedidos de categoria dentro (a tela principal de inscrições).</summary>
    [HttpGet("temporadas/{temporadaId:guid}/inscricoes")]
    public async Task<ActionResult> ListarDaTemporada(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await _temporadaRepository.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return NotFound();

        var fichas = await _repository.ListarFichasPorTemporadaAsync(temporadaId, ct);
        return CustomResponse(fichas.Select(i => new
        {
            Id = i.Id,
            Pessoa = ProjetarPessoa(i.Pessoa, temporada.Ano),
            Dados = new { i.AlturaCm, i.PesoKg, i.Posicao, i.PossuiPlanoSaude, i.NomePlanoSaude },
            i.DataEnvio,
            Financeiro = ProjetarFinanceiro(i),
            Categorias = i.Categorias
                .OrderBy(c => c.TemporadaCategoria.Nome)
                .Select(c => new
                {
                    c.Id,
                    c.TemporadaCategoriaId,
                    Nome = c.TemporadaCategoria.Nome,
                    c.Status,
                    c.JustificativaExcecao,
                    c.RecusadaEm,
                    c.MotivoRecusa,
                    ForaDoEsperado = c.TemporadaCategoria.DescreverExcecoes(i.Pessoa.Nascimento, i.Pessoa.Sexo, temporada.Ano)
                })
        }));
    }

    [HttpGet("temporadas-categorias/{temporadaCategoriaId:guid}/inscricoes")]
    public async Task<ActionResult> Listar(Guid temporadaCategoriaId, CancellationToken ct)
    {
        var pedidos = await _repository.ListarPorTemporadaCategoriaAsync(temporadaCategoriaId, ct);
        return CustomResponse(pedidos.Select(ic =>
        {
            var i = ic.Inscricao;
            var p = i.Pessoa;
            var tc = ic.TemporadaCategoria;
            var ano = tc.Temporada.Ano;
            return new
            {
                ic.Id,
                InscricaoId = i.Id,
                Pessoa = ProjetarPessoa(p, ano),
                Ficha = new { i.AlturaCm, i.PesoKg, i.Posicao, i.PossuiPlanoSaude, i.NomePlanoSaude },
                i.DataEnvio,
                ic.Status,
                Financeiro = ProjetarFinanceiro(i),
                ic.JustificativaExcecao,
                ic.RecusadaEm,
                ic.MotivoRecusa,
                ForaDoEsperado = tc.DescreverExcecoes(p.Nascimento, p.Sexo, ano)
            };
        }));
    }

    [HttpPost("inscricoes")]
    public async Task<ActionResult> Enviar([FromBody] EnviarInscricaoCommand command)
    {
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("inscricoes-categorias/{id:guid}/aprovar")]
    public async Task<ActionResult> Aprovar(Guid id, [FromBody] AprovarInscricaoCategoriaCommand command)
    {
        command.InscricaoCategoriaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("inscricoes-categorias/{id:guid}/recusar")]
    public async Task<ActionResult> Recusar(Guid id, [FromBody] RecusarInscricaoCategoriaCommand command)
    {
        command.InscricaoCategoriaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("inscricoes/{inscricaoId:guid}/pagamentos/taxa")]
    public async Task<ActionResult> RegistrarPagamentoTaxa(Guid inscricaoId, [FromBody] RegistrarPagamentoTaxaCommand command)
    {
        command.InscricaoId = inscricaoId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("inscricoes/{inscricaoId:guid}/pagamentos/saldo")]
    public async Task<ActionResult> RegistrarPagamentoSaldo(Guid inscricaoId, [FromBody] RegistrarPagamentoSaldoCommand command)
    {
        command.InscricaoId = inscricaoId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}

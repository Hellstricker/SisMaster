using Microsoft.AspNetCore.Authorization;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApps.WebApi.Seguranca;
using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Importacao;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Excecoes;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api")]
[Authorize(Policy = Politicas.Gestao)]
public class InscricoesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IInscricaoRepository _repository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly ExportacaoDeInscricoesQuery _exportacao;
    private readonly ImportacaoDeInscricoes _importacao;
    private readonly UniaoDeCadastros _uniao;

    public InscricoesController(IMediatorHandler mediator, IInscricaoRepository repository, ITemporadaRepository temporadaRepository,
        ExportacaoDeInscricoesQuery exportacao, ImportacaoDeInscricoes importacao, UniaoDeCadastros uniao)
    {
        _uniao = uniao;
        _mediator = mediator;
        _repository = repository;
        _temporadaRepository = temporadaRepository;
        _exportacao = exportacao;
        _importacao = importacao;
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
        Pagamentos = i.Pagamentos.OrderBy(p => p.Data).ThenBy(p => p.Id).Select(p => new
        {
            p.Id, p.Tipo, p.Valor, Data = p.Data.ToString("yyyy-MM-dd"),
            Estornado = !p.Ativo, p.EstornadoEm, p.MotivoEstorno,
        })
    };

    private static object ProjetarPessoa(Pessoa p, int anoTemporada) => new
    {
        p.Id,
        p.Nome,
        Cpf = p.Cpf.ToString(),
        Nascimento = p.Nascimento.ToString("yyyy-MM-dd"),
        Idade = anoTemporada - p.Nascimento.Year,
        p.Sexo,
        p.Perfil,
        p.Email,
        p.Telefone
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
            i.Protocolo,
            i.Origem,
            ContaLigada = i.Pessoa.ContaId != null,
            Autorizacoes = new { Dados = RelogioLocal.DeUtc(i.ConsentimentoLgpdEm), Imagem = RelogioLocal.DeUtc(i.ConsentimentoImagemEm), i.VersaoTermos },
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
        command.PelaDiretoria = true; // esta rota é da gestão (política Gestao); a inscrição pública terá rota própria
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [IdEh(Recurso.InscricaoCategoria)]
    [HttpPatch("inscricoes-categorias/{id:guid}/aprovar")]
    public async Task<ActionResult> Aprovar(Guid id, [FromBody] AprovarInscricaoCategoriaCommand command)
    {
        command.InscricaoCategoriaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [IdEh(Recurso.InscricaoCategoria)]
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

    /// <summary>Corrige altura, peso, posição, plano de saúde, e-mail e telefone da ficha.</summary>
    [HttpPatch("inscricoes/{inscricaoId:guid}/dados")]
    public async Task<ActionResult> EditarDados(Guid inscricaoId, [FromBody] EditarDadosFichaCommand command)
    {
        command.InscricaoId = inscricaoId;
        return CustomResponse(await _mediator.EnviarComando(command));
    }

    /// <summary>Estorna um pagamento (fica no histórico, com motivo).</summary>
    [HttpPost("inscricoes/{inscricaoId:guid}/pagamentos/{pagamentoId:guid}/estornar")]
    public async Task<ActionResult> Estornar(Guid inscricaoId, Guid pagamentoId, [FromBody] EstornarPagamentoCommand command)
    {
        command.InscricaoId = inscricaoId;
        command.PagamentoId = pagamentoId;
        return CustomResponse(await _mediator.EnviarComando(command));
    }

    [IdEh(Recurso.InscricaoCategoria)]
    [HttpPatch("inscricoes-categorias/{id:guid}/desfazer-aprovacao")]
    public async Task<ActionResult> DesfazerAprovacao(Guid id, [FromBody] DesfazerAprovacaoCommand command)
    {
        command.InscricaoCategoriaId = id;
        return CustomResponse(await _mediator.EnviarComando(command));
    }

    [IdEh(Recurso.InscricaoCategoria)]
    [HttpPatch("inscricoes-categorias/{id:guid}/reabrir")]
    public async Task<ActionResult> Reabrir(Guid id)
    {
        return CustomResponse(await _mediator.EnviarComando(new ReabrirPedidoCommand { InscricaoCategoriaId = id }));
    }

    /// <summary>Nome, CPF, nascimento e sexo definem a elegibilidade: só o Administrador corrige, com registro de auditoria.</summary>
    [Authorize(Policy = Politicas.SomenteAdministrador)]
    [HttpPatch("pessoas/{pessoaId:guid}/identificacao")]
    public async Task<ActionResult> CorrigirIdentificacao(Guid pessoaId, [FromBody] CorrigirIdentificacaoCommand command)
    {
        command.PessoaId = pessoaId;
        return CustomResponse(await _mediator.EnviarComando(command));
    }

    public sealed record CorpoDaUniao(string? CpfDuplicada);

    /// <summary>Une um cadastro duplicado (pelo CPF) a este: as fichas e a conta do site passam para este, e o duplicado é apagado. Só o Administrador.</summary>
    [Authorize(Policy = Politicas.SomenteAdministrador)]
    [HttpPost("pessoas/{pessoaId:guid}/unir")]
    public async Task<ActionResult> Unir(Guid pessoaId, [FromBody] CorpoDaUniao corpo, CancellationToken ct)
    {
        try
        {
            await _uniao.UnirAsync(pessoaId, corpo.CpfDuplicada ?? string.Empty, ct);
            return CustomResponse();
        }
        catch (DomainException ex) { AdicionarErro(ex.Message); return CustomResponse(); }
    }

    /// <summary>CSV das fichas do filtro atual. CPF, contato e dados da ficha só saem se pedidos; cada exportação é registrada.</summary>
    [HttpGet("temporadas/{temporadaId:guid}/inscricoes/exportar")]
    public async Task<IActionResult> Exportar(Guid temporadaId, [FromQuery] string? status, [FromQuery] Guid? categoriaId, [FromQuery] string? busca,
        [FromQuery] bool cpf = false, [FromQuery] bool contato = false, [FromQuery] bool ficha = true, CancellationToken ct = default)
    {
        StatusInscricaoCategoria? situacao = null;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("Todas", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<StatusInscricaoCategoria>(status, true, out var s)) return BadRequest("Situação inválida.");
            situacao = s;
        }
        var arquivo = await _exportacao.ExecutarAsync(temporadaId, new FiltroDeExportacao(situacao, categoriaId, busca), new ColunasDeExportacao(cpf, contato, ficha), ct);
        if (arquivo is null) return NotFound();
        return File(arquivo.Conteudo, "text/csv; charset=utf-8", arquivo.Nome);
    }

    public sealed record CorpoDaImportacao(string? Csv, bool ConsentimentoConfirmado = false);

    /// <summary>Confere a planilha sem gravar nada: o que seria criado, ignorado ou tem erro.</summary>
    [HttpPost("temporadas/{temporadaId:guid}/inscricoes/importacao/previa")]
    public async Task<ActionResult> PreviaDaImportacao(Guid temporadaId, [FromBody] CorpoDaImportacao corpo, CancellationToken ct)
    {
        try
        {
            var previa = await _importacao.PreviaAsync(temporadaId, corpo.Csv ?? string.Empty, ct);
            return previa is null ? NotFound() : CustomResponse(previa);
        }
        catch (DomainException ex) { AdicionarErro(ex.Message); return CustomResponse(); }
    }

    /// <summary>Cria as fichas das linhas sem erro. Exige a confirmação de que a diretoria tem o consentimento LGPD das pessoas.</summary>
    [HttpPost("temporadas/{temporadaId:guid}/inscricoes/importacao")]
    public async Task<ActionResult> Importar(Guid temporadaId, [FromBody] CorpoDaImportacao corpo, CancellationToken ct)
    {
        if (!corpo.ConsentimentoConfirmado)
        {
            AdicionarErro("Confirme que a associação tem o consentimento LGPD das pessoas da planilha.");
            return CustomResponse();
        }
        try
        {
            var resultado = await _importacao.ImportarAsync(temporadaId, corpo.Csv ?? string.Empty, ct);
            return resultado is null ? NotFound() : CustomResponse(resultado);
        }
        catch (DomainException ex) { AdicionarErro(ex.Message); return CustomResponse(); }
    }

    [HttpPost("inscricoes/{inscricaoId:guid}/pagamentos/saldo")]
    public async Task<ActionResult> RegistrarPagamentoSaldo(Guid inscricaoId, [FromBody] RegistrarPagamentoSaldoCommand command)
    {
        command.InscricaoId = inscricaoId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}

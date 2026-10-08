using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

public interface IInscricaoRepository : IRepository<Inscricao>
{
    /// <summary>Carrega a ficha com Pessoa, Categorias e Pagamentos.</summary>
    Task<Inscricao?> ObterPorIdAsync(Guid inscricaoId, CancellationToken ct = default);

    /// <summary>Ficha que contém o pedido, com Pessoa, Categorias (e TemporadaCategoria.Temporada) e Pagamentos.</summary>
    Task<Inscricao?> ObterPorCategoriaIdAsync(Guid inscricaoCategoriaId, CancellationToken ct = default);

    /// <summary>Todas as fichas da temporada com Categorias (e TemporadaCategoria) e Pagamentos, para gerar cobranças.</summary>
    Task<IReadOnlyList<Inscricao>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Fichas da temporada (somente leitura) com Pessoa, Pagamentos e os pedidos de categoria.</summary>
    Task<IReadOnlyList<Inscricao>> ListarFichasPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    Task<IReadOnlyList<InscricaoCategoria>> ListarPorTemporadaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default);

    /// <summary>Existe pedido da pessoa nesta categoria que não tenha sido recusado?</summary>
    Task<bool> ExisteAtivaAsync(Guid pessoaId, Guid temporadaCategoriaId, CancellationToken ct = default);

    void Adicionar(Inscricao inscricao);
    void AdicionarPagamento(PagamentoInscricao pagamento);
}

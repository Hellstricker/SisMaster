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

    /// <summary>Alguma ficha da temporada já teve a cobrança gerada?</summary>
    Task<bool> ExisteCobrancaGeradaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>A inscrição enviada pelo site com este código (rastreada, com os pedidos de categoria), se houver: torna o envio idempotente.</summary>
    Task<Inscricao?> ObterPorCodigoDeEnvioAsync(Guid codigo, CancellationToken ct = default);

    /// <summary>A última ficha da pessoa (para pré-preencher a ficha esportiva da próxima inscrição), somente leitura.</summary>
    Task<Inscricao?> ObterUltimaDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Todas as fichas da pessoa (somente leitura), da mais recente à mais antiga, com os pedidos de categoria e a temporada.</summary>
    Task<IReadOnlyList<Inscricao>> ListarDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>A ficha da pessoa (somente leitura) com pedidos, temporada, associação e pagamentos; nulo se não existir ou for de outra pessoa.</summary>
    Task<Inscricao?> ObterDaPessoaAsync(Guid inscricaoId, Guid pessoaId, CancellationToken ct = default);

    /// <summary>Equipe de cada pedido efetivado já colocado em equipe: pedido -> nome da equipe.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListarEquipesDosPedidosAsync(IReadOnlyCollection<Guid> inscricaoCategoriaIds, CancellationToken ct = default);

    /// <summary>Todas as fichas da pessoa para alteração (rastreadas), com os pedidos de categoria.</summary>
    Task<IReadOnlyList<Inscricao>> ListarParaUniaoAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Os pedidos ativos (não recusados) da pessoa na temporada, com o protocolo da ficha: categoria -> protocolo (ou "—" se sem protocolo).</summary>
    Task<IReadOnlyDictionary<Guid, string>> ListarCategoriasAtivasDaPessoaAsync(Guid pessoaId, Guid temporadaId, CancellationToken ct = default);

    /// <summary>Quantas fichas a temporada já tem com protocolo (base do próximo número).</summary>
    Task<int> ContarProtocolosAsync(Guid temporadaId, CancellationToken ct = default);

    void Adicionar(Inscricao inscricao);
    void AdicionarPagamento(PagamentoInscricao pagamento);
}

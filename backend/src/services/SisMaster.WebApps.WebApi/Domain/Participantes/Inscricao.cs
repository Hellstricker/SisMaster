using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Participantes;

/// <summary>
/// A ficha de um envio: dados compartilhados por todas as categorias pretendidas naquele envio, e
/// também a conta financeira da pessoa na temporada:
///  1. taxa fixa da temporada, paga uma vez (efetiva as categorias aprovadas);
///  2. após o fim das inscrições, a cobrança final = total das categorias − desconto por quantidade;
///  3. saldo = valor final − tudo que já foi pago.
/// Cada solicitação (inclusive nova após recusa) é uma nova Inscricao.
/// </summary>
public class Inscricao : Entity, IAggregateRoot
{
    public Guid PessoaId { get; private set; }
    public Guid TemporadaId { get; private set; }
    public int? AlturaCm { get; private set; }
    public int? PesoKg { get; private set; }
    public string? Posicao { get; private set; }
    public bool PossuiPlanoSaude { get; private set; }
    public string? NomePlanoSaude { get; private set; }
    public DateTime DataEnvio { get; private set; }

    /// <summary>Consentimento LGPD: guarda o instante exato, não só um booleano.</summary>
    public DateTime ConsentimentoLgpdEm { get; private set; }

    // Cobrança final (nula até a diretoria gerar as cobranças da temporada)
    public decimal? ValorTotal { get; private set; }
    public decimal? ValorDesconto { get; private set; }
    public DateTime? CobrancaGeradaEm { get; private set; }

    public Pessoa Pessoa { get; private set; } = null!;

    private readonly List<InscricaoCategoria> _categorias = [];
    public IReadOnlyCollection<InscricaoCategoria> Categorias => _categorias.AsReadOnly();

    private readonly List<PagamentoInscricao> _pagamentos = [];
    public IReadOnlyCollection<PagamentoInscricao> Pagamentos => _pagamentos.AsReadOnly();

    protected Inscricao() { }

    public Inscricao(Guid pessoaId, Guid temporadaId, int? alturaCm, int? pesoKg, string? posicao,
        bool possuiPlanoSaude, string? nomePlanoSaude, bool consentimentoLgpd)
    {
        Validacoes.ValidarSeFalso(consentimentoLgpd, "O consentimento LGPD é obrigatório");
        if (alturaCm is not null)
            Validacoes.ValidarMinimoMaximo(alturaCm.Value, 100, 260, "Altura deve estar entre 100 e 260 cm");
        if (pesoKg is not null)
            Validacoes.ValidarMinimoMaximo(pesoKg.Value, 30, 250, "Peso deve estar entre 30 e 250 kg");
        if (possuiPlanoSaude)
            Validacoes.ValidarSeVazio(nomePlanoSaude ?? string.Empty, "Informe o nome do plano de saúde");

        PessoaId = pessoaId;
        TemporadaId = temporadaId;
        AlturaCm = alturaCm;
        PesoKg = pesoKg;
        Posicao = string.IsNullOrWhiteSpace(posicao) ? null : posicao.Trim();
        PossuiPlanoSaude = possuiPlanoSaude;
        NomePlanoSaude = possuiPlanoSaude ? nomePlanoSaude!.Trim() : null;
        DataEnvio = DateTime.UtcNow;
        ConsentimentoLgpdEm = DataEnvio;
    }

    // ---- Situação financeira

    public bool TaxaPaga => _pagamentos.Any(p => p.Tipo == TipoPagamento.Taxa);
    public decimal TotalPago => _pagamentos.Sum(p => p.Valor);
    public bool CobrancaGerada => CobrancaGeradaEm is not null;

    /// <summary>Total das categorias menos o desconto. Nulo até a cobrança ser gerada.</summary>
    public decimal? ValorFinal => CobrancaGerada ? ValorTotal - ValorDesconto : null;

    /// <summary>Valor final − já pago (a taxa é abatida). Negativo = pagou a mais. Nulo até gerar a cobrança.</summary>
    public decimal? Saldo => ValorFinal - TotalPago;

    // ---- Categorias

    public InscricaoCategoria AdicionarCategoria(Guid temporadaCategoriaId)
    {
        if (_categorias.Any(c => c.TemporadaCategoriaId == temporadaCategoriaId))
            throw new DomainException("Categoria já incluída nesta inscrição");

        var ic = new InscricaoCategoria(Id, temporadaCategoriaId);
        _categorias.Add(ic);
        return ic;
    }

    /// <summary>
    /// Aprova o pedido. Se a taxa da ficha já foi paga, o pedido aprovado já é efetivado
    /// (a taxa é paga uma vez por ficha, não por categoria).
    /// </summary>
    public void AprovarCategoria(Guid inscricaoCategoriaId, string? justificativaExcecao, bool foraDoEsperado)
    {
        var ic = ObterCategoria(inscricaoCategoriaId);
        ic.Aprovar(justificativaExcecao, foraDoEsperado);
        if (TaxaPaga) ic.Efetivar();
    }

    private InscricaoCategoria ObterCategoria(Guid id) =>
        _categorias.FirstOrDefault(c => c.Id == id)
        ?? throw new DomainException("Inscrição não encontrada nesta ficha");

    // ---- Pagamentos

    /// <summary>Registra a taxa fixa da temporada e efetiva as categorias já aprovadas.</summary>
    public PagamentoInscricao RegistrarPagamentoTaxa(decimal? taxaDaTemporada, DateOnly data)
    {
        if (taxaDaTemporada is null || taxaDaTemporada <= 0)
            throw new DomainException("A taxa de inscrição da temporada ainda não foi definida");
        if (TaxaPaga)
            throw new DomainException("A taxa desta inscrição já foi paga");

        var aguardando = _categorias.Where(c => c.Status == StatusInscricaoCategoria.AguardandoPagamento).ToList();
        if (aguardando.Count == 0)
            throw new DomainException("Nenhuma categoria aprovada aguardando pagamento nesta inscrição");

        var pagamento = new PagamentoInscricao(Id, TipoPagamento.Taxa, taxaDaTemporada.Value, data);
        _pagamentos.Add(pagamento);
        foreach (var ic in aguardando) ic.Efetivar();
        return pagamento;
    }

    /// <summary>Registra pagamento (parcial ou total) do valor final cobrado.</summary>
    public PagamentoInscricao RegistrarPagamentoSaldo(decimal valor, DateOnly data)
    {
        if (!CobrancaGerada)
            throw new DomainException("A cobrança final ainda não foi gerada");
        if (valor > Saldo)
            throw new DomainException($"Valor maior que o saldo em aberto ({Saldo:N2})");

        var pagamento = new PagamentoInscricao(Id, TipoPagamento.Saldo, valor, data);
        _pagamentos.Add(pagamento);
        return pagamento;
    }

    // ---- Cobrança final

    /// <summary>Categorias que entram na cobrança: todas as que não foram recusadas.</summary>
    public IReadOnlyList<InscricaoCategoria> CategoriasCobradas => _categorias.Where(c => c.Ativa).ToList();

    public void GerarCobranca(decimal total, decimal desconto)
    {
        Validacoes.ValidarSeMenorQue(total, 0m, "Total inválido");
        Validacoes.ValidarSeMenorQue(desconto, 0m, "Desconto inválido");
        ValorTotal = total;
        ValorDesconto = Math.Min(desconto, total);
        CobrancaGeradaEm = DateTime.UtcNow;
    }
}

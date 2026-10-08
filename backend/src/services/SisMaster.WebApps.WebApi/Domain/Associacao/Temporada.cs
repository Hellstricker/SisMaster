using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public class Temporada : Entity
{
    public Guid CampeonatoId { get; private set; }
    public int Ano { get; private set; }
    public StatusTemporada Status { get; private set; }
    public DateTime DataInicioInscricoes { get; private set; }
    public DateTime DataFimInscricoes { get; private set; }

    /// <summary>Liga/desliga a aceitação de inscrições, independente do Status (exceto temporada encerrada).</summary>
    public bool InscricoesHabilitadas { get; private set; }

    /// <summary>Pontos extras por atleta que cumpriu o rodízio obrigatório. Zero desliga a bonificação.</summary>
    public decimal ValorBonificacaoPorAtleta { get; private set; }

    /// <summary>Taxa fixa paga uma vez por ficha ao ter a inscrição aceita. Nulo = a definir.</summary>
    public decimal? TaxaInscricao { get; private set; }

    public Campeonato Campeonato { get; private set; } = null!;

    private List<TemporadaCategoria> _categorias = [];
    public IReadOnlyCollection<TemporadaCategoria> Categorias => _categorias.AsReadOnly();

    private readonly List<DescontoPorCombinacao> _descontos = [];
    public IReadOnlyCollection<DescontoPorCombinacao> Descontos => _descontos.AsReadOnly();

    /// <summary>
    /// Quando o cadastro de fases foi encerrado: a tabela de jogos do campeonato é montada a partir das fases.
    /// Encerrado: não se cria, exclui nem reordena fase, nem muda tipo/parâmetros (só o nome).
    /// </summary>
    public DateTime? CadastroFasesEncerradoEm { get; private set; }

    /// <summary>Quando a tabela de jogos foi gerada (Tela 8/9). A partir daí o encerramento é definitivo.</summary>
    public DateTime? TabelaJogosGeradaEm { get; private set; }

    public bool CadastroFasesEncerrado => CadastroFasesEncerradoEm is not null;
    public bool TabelaJogosGerada => TabelaJogosGeradaEm is not null;

    protected Temporada() { }

    public Temporada(Guid campeonatoId, int ano, DateTime dataInicioInscricoes, DateTime dataFimInscricoes)
    {
        Validacoes.ValidarMinimoMaximo(ano, 2000, 2100, "Ano inválido para a temporada");
        if (dataFimInscricoes <= dataInicioInscricoes)
            throw new DomainException("Data fim das inscrições deve ser posterior à data início");

        CampeonatoId = campeonatoId;
        Ano = ano;
        DataInicioInscricoes = dataInicioInscricoes;
        DataFimInscricoes = dataFimInscricoes;
        Status = StatusTemporada.InscricoesAbertas;
        InscricoesHabilitadas = true;
    }

    public void EncerrarInscricoes()
    {
        if (Status != StatusTemporada.InscricoesAbertas)
            throw new DomainException("Inscrições já estão encerradas");
        Status = StatusTemporada.InscricoesEncerradas;
        InscricoesHabilitadas = false;
    }

    public void Iniciar()
    {
        if (Status != StatusTemporada.InscricoesEncerradas)
            throw new DomainException("Temporada só pode ser iniciada após encerrar as inscrições");
        Status = StatusTemporada.EmAndamento;
    }

    public void Encerrar()
    {
        if (Status != StatusTemporada.EmAndamento)
            throw new DomainException("Temporada só pode ser encerrada se estiver em andamento");
        Status = StatusTemporada.Encerrada;
        InscricoesHabilitadas = false;
    }

    public void ValidarPermiteFormarEquipes()
    {
        if (Status == StatusTemporada.InscricoesAbertas)
            throw new DomainException("As equipes só podem ser formadas depois de encerradas as inscrições da temporada");
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de equipes");
    }

    /// <summary>Fases só podem ser planejadas depois de encerradas as inscrições e antes de a temporada encerrar.</summary>
    public void ValidarPermiteCadastrarFases()
    {
        if (Status == StatusTemporada.InscricoesAbertas)
            throw new DomainException("As fases só podem ser cadastradas depois de encerradas as inscrições da temporada");
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de fases");
    }

    public void ValidarCadastroFasesAberto()
    {
        ValidarPermiteCadastrarFases();
        if (CadastroFasesEncerrado)
            throw new DomainException("O cadastro de fases está encerrado");
    }

    public void EncerrarCadastroFases(bool existeFase)
    {
        ValidarCadastroFasesAberto();
        if (!existeFase)
            throw new DomainException("Cadastre ao menos uma fase antes de encerrar o cadastro");
        CadastroFasesEncerradoEm = DateTime.UtcNow;
    }

    public void ValidarPodeGerarTabelaJogos()
    {
        ValidarPermiteCadastrarFases();
        if (!CadastroFasesEncerrado)
            throw new DomainException("Encerre o cadastro de fases antes de montar a tabela de jogos");
        if (TabelaJogosGerada)
            throw new DomainException("A tabela de jogos já foi gerada");
    }

    public void MarcarTabelaJogosGerada()
    {
        ValidarPodeGerarTabelaJogos();
        TabelaJogosGeradaEm = DateTime.UtcNow;
    }

    public void ReabrirCadastroFases()
    {
        ValidarPermiteCadastrarFases();
        if (!CadastroFasesEncerrado)
            throw new DomainException("O cadastro de fases não está encerrado");
        if (TabelaJogosGerada)
            throw new DomainException("Não é possível reabrir: a tabela de jogos já foi gerada");
        CadastroFasesEncerradoEm = null;
    }

    public void ValidarAceitaInscricoes()
    {
        if (!InscricoesHabilitadas)
            throw new DomainException("As inscrições desta temporada não estão habilitadas");
    }

    public void AlterarInscricoesHabilitadas(bool habilitadas)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de inscrições");
        InscricoesHabilitadas = habilitadas;
    }

    public void DefinirTaxaInscricao(decimal? taxa)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de taxa");
        if (taxa is < 0)
            throw new DomainException("A taxa de inscrição não pode ser negativa");
        TaxaInscricao = taxa;
    }

    /// <summary>
    /// Define (ou atualiza) o desconto de uma combinação exata de categorias da temporada (ao menos 2).
    /// Devolve a entidade e se ela é nova, para o repositório adicioná-la explicitamente.
    /// </summary>
    public (DescontoPorCombinacao Desconto, bool Novo) DefinirDesconto(IReadOnlyCollection<Guid> temporadaCategoriaIds, TipoDesconto tipo, decimal valor)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de descontos");

        var ids = temporadaCategoriaIds.Distinct().ToList();
        if (ids.Count < 2)
            throw new DomainException("O desconto vale para uma combinação de ao menos 2 categorias");
        if (ids.Any(id => _categorias.All(c => c.Id != id)))
            throw new DomainException("Há categoria que não pertence a esta temporada");

        var existente = _descontos.FirstOrDefault(d => d.Cobre(ids));
        if (existente is not null)
        {
            existente.Atualizar(tipo, valor);
            return (existente, false);
        }

        var novo = new DescontoPorCombinacao(Id, ids, tipo, valor);
        _descontos.Add(novo);
        return (novo, true);
    }

    public DescontoPorCombinacao RemoverDesconto(Guid descontoId)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de descontos");

        var desconto = _descontos.FirstOrDefault(d => d.Id == descontoId)
            ?? throw new DomainException("Desconto não encontrado nesta temporada");
        _descontos.Remove(desconto);
        return desconto;
    }

    /// <summary>
    /// Desconto de quem joga exatamente estas categorias: só vale a combinação idêntica configurada; sem ela, não há desconto.
    /// </summary>
    public decimal CalcularDesconto(IReadOnlyCollection<Guid> temporadaCategoriaIds, decimal total) =>
        _descontos.FirstOrDefault(d => d.Cobre(temporadaCategoriaIds))?.Calcular(total) ?? 0m;

    public void ValidarPodeGerarCobranca()
    {
        if (InscricoesHabilitadas)
            throw new DomainException("Encerre/desabilite as inscrições antes de gerar as cobranças");
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita geração de cobranças");
    }

    public void ConfigurarBonificacaoPorAtleta(decimal valor)
    {
        if (valor < 0)
            throw new DomainException("Valor da bonificação não pode ser negativo");
        ValorBonificacaoPorAtleta = valor;
    }

    public TemporadaCategoria AdicionarCategoria(Categoria categoria)
    {
        if (Status != StatusTemporada.InscricoesAbertas)
            throw new DomainException("Categorias só podem ser adicionadas com as inscrições abertas");
        if (_categorias.Any(c => c.CategoriaId == categoria.Id))
            throw new DomainException("Categoria já adicionada nesta temporada");

        var tc = new TemporadaCategoria(Id, categoria);
        _categorias.Add(tc);
        return tc;
    }

    /// <summary>Tira a categoria da temporada. Quem chama confere antes que nada depende dela (pedidos, equipes, fases).</summary>
    public TemporadaCategoria RemoverCategoria(Guid categoriaId)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de categorias");

        var tc = _categorias.FirstOrDefault(c => c.CategoriaId == categoriaId)
            ?? throw new DomainException("Categoria não está vinculada a esta temporada");
        _categorias.Remove(tc);
        return tc;
    }

    public void ConfigurarElegibilidadeCategoria(Guid categoriaId, int idadeMinima, Sexo? sexo, bool aceitaAbaixoIdadeMinima)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de regras");

        var tc = _categorias.FirstOrDefault(c => c.CategoriaId == categoriaId)
            ?? throw new DomainException("Categoria não está vinculada a esta temporada");
        tc.ConfigurarElegibilidade(idadeMinima, sexo, aceitaAbaixoIdadeMinima);
    }

    public void DefinirValorCategoria(Guid categoriaId, decimal? valor)
    {
        if (Status == StatusTemporada.Encerrada)
            throw new DomainException("Temporada encerrada não aceita alteração de valores");

        var tc = _categorias.FirstOrDefault(c => c.CategoriaId == categoriaId)
            ?? throw new DomainException("Categoria não está vinculada a esta temporada");
        tc.DefinirValor(valor);
    }
}

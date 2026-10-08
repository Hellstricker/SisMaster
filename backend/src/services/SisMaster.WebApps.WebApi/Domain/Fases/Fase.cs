using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Domain.Fases;

/// <summary>
/// Fase de uma categoria da temporada. O tipo define a estrutura de jogos (ver <see cref="EstruturaFase"/>):
/// pontos corridos e grupos jogam em turnos; grupos têm número de grupos e distribuição das equipes; mata-mata
/// tem número de confrontos e "melhor de N". Cada fase também traz a regra de quem se classifica para a próxima.
/// A ordem é sequencial (1, 2, 3…) e gerida pela <see cref="SequenciaDeFases"/>. O status NÃO é manual:
/// acompanha os jogos da fase (Tela 8/9).
/// </summary>
public class Fase : Entity, IAggregateRoot
{
    public Guid TemporadaCategoriaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public TipoFase Tipo { get; private set; }

    public int? NumeroTurnos { get; private set; }
    public int? NumeroGrupos { get; private set; }
    public DistribuicaoEquipes? Distribuicao { get; private set; }
    public int? JogosPorConfronto { get; private set; }
    public int? NumeroConfrontos { get; private set; }

    /// <summary>Quantos se classificam (por grupo, em Grupos; no total, em Pontos corridos). Nulo = todos.</summary>
    public int? ClassificadosPrimeiros { get; private set; }

    /// <summary>Só em Grupos: melhores (N+1)º colocados que também se classificam.</summary>
    public int MelhoresExtras { get; private set; }

    /// <summary>De onde vêm os classificados. Opcional, da mesma categoria e antes desta na sequência.</summary>
    public Guid? FaseAnteriorId { get; private set; }

    public TemporadaCategoria TemporadaCategoria { get; private set; } = null!;

    /// <summary>
    /// Distribuição manual: o grupo (1 = A, 2 = B…) de cada vaga, na ordem das vagas, separados por vírgula ("1,2,2,1").
    /// Só existe em fase de grupos com distribuição Manual; nulo = blocos seguidos. Zera quando a estrutura da fase muda.
    /// </summary>
    public string? DistribuicaoManual { get; private set; }

    public IReadOnlyList<int>? GruposManual =>
        string.IsNullOrEmpty(DistribuicaoManual) ? null : DistribuicaoManual.Split(',').Select(int.Parse).ToList();

    // O status da fase não é um campo: é derivado dos jogos (ver StatusFaseDerivado).

    protected Fase() { }

    public Fase(Guid temporadaCategoriaId, int ordem, string nome, TipoFase tipo, EstruturaFase estrutura, Guid? faseAnteriorId, int equipesDaCategoria)
    {
        Validacoes.ValidarMinimoMaximo(ordem, 1, 100, "Ordem inválida");
        TemporadaCategoriaId = temporadaCategoriaId;
        Ordem = ordem;
        DefinirEstrutura(nome, tipo, estrutura, faseAnteriorId, equipesDaCategoria);
    }

    /// <summary>
    /// Edita a fase. Com o cadastro de fases encerrado só o nome pode mudar; qualquer outra alteração é recusada.
    /// </summary>
    public void Editar(bool cadastroEncerrado, string nome, TipoFase tipo, EstruturaFase estrutura, Guid? faseAnteriorId, int equipesDaCategoria)
    {
        if (!cadastroEncerrado)
        {
            DefinirEstrutura(nome, tipo, estrutura, faseAnteriorId, equipesDaCategoria);
            return;
        }

        var nova = EstruturaFase.Normalizar(tipo, estrutura, equipesDaCategoria);
        if (tipo != Tipo || nova != Estrutura || faseAnteriorId != FaseAnteriorId)
            throw new DomainException("O cadastro de fases está encerrado: só o nome da fase pode ser alterado");

        ValidarNome(nome);
        Nome = nome.Trim();
    }

    /// <summary>A estrutura atual da fase, no mesmo formato aceito pela criação/edição.</summary>
    public EstruturaFase Estrutura =>
        new(NumeroTurnos, NumeroGrupos, Distribuicao, JogosPorConfronto, NumeroConfrontos, ClassificadosPrimeiros, MelhoresExtras);

    /// <summary>
    /// A estrutura obrigatória do tipo está toda preenchida? Fases cadastradas antes de existirem os campos
    /// de grupos/confrontos/distribuição podem estar incompletas e precisam ser editadas antes de encerrar o cadastro.
    /// </summary>
    public bool EstruturaCompleta => Tipo switch
    {
        TipoFase.MataMata => JogosPorConfronto is not null && NumeroConfrontos is not null,
        TipoFase.Grupos => NumeroTurnos is not null && NumeroGrupos is not null && Distribuicao is not null,
        _ => NumeroTurnos is not null
    };

    internal void DefinirOrdem(int ordem) => Ordem = ordem;

    /// <summary>Define o grupo de cada vaga (na ordem das vagas). Cada grupo precisa de ao menos 2 equipes.</summary>
    public void DefinirDistribuicaoManual(IReadOnlyList<int> grupos)
    {
        if (Tipo != TipoFase.Grupos || Distribuicao != DistribuicaoEquipes.Manual)
            throw new DomainException("Só uma fase de grupos com distribuição manual aceita a escolha dos grupos");

        var total = NumeroGrupos ?? 0;
        if (grupos.Any(g => g < 1 || g > total))
            throw new DomainException($"Os grupos devem estar entre A e {Grupo(total)}");
        for (var g = 1; g <= total; g++)
        {
            if (grupos.Count(x => x == g) < 2)
                throw new DomainException($"O grupo {Grupo(g)} precisa de ao menos 2 equipes");
        }

        DistribuicaoManual = string.Join(',', grupos);
    }

    private static string Grupo(int ordem) => ((char)('A' + ordem - 1)).ToString();

    private void DefinirEstrutura(string nome, TipoFase tipo, EstruturaFase estrutura, Guid? faseAnteriorId, int equipesDaCategoria)
    {
        ValidarNome(nome);
        if (faseAnteriorId == Id)
            throw new DomainException("Uma fase não pode vir dela mesma");

        var e = EstruturaFase.Normalizar(tipo, estrutura, equipesDaCategoria);

        // A escolha manual dos grupos só vale para a estrutura em que foi feita.
        var mudouEstrutura = tipo != Tipo || e != Estrutura || faseAnteriorId != FaseAnteriorId;

        Nome = nome.Trim();
        Tipo = tipo;
        NumeroTurnos = e.NumeroTurnos;
        NumeroGrupos = e.NumeroGrupos;
        Distribuicao = e.Distribuicao;
        JogosPorConfronto = e.JogosPorConfronto;
        NumeroConfrontos = e.NumeroConfrontos;
        ClassificadosPrimeiros = e.ClassificadosPrimeiros;
        MelhoresExtras = e.MelhoresExtras;
        FaseAnteriorId = faseAnteriorId;
        if (mudouEstrutura) DistribuicaoManual = null;
    }

    private static void ValidarNome(string nome)
    {
        Validacoes.ValidarSeVazio(nome, "Nome da fase é obrigatório");
        Validacoes.ValidarTamanho(nome.Trim(), 1, 100, "Nome da fase deve ter até 100 caracteres");
    }
}

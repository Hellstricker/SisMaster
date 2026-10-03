using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Time.VOs;

namespace SisMaster.WebApps.WebApi.Domain.Time;

public class Jogador : Entity
{
    public Guid TimeId { get; private set; }
    public int Numero { get; private set; }
    public bool Ativo { get; private set; }
    public Pessoa Pessoa { get; private set; }

    public Time Time { get; private set; } = null!;

    protected Jogador() { Pessoa = null!; }

    public Jogador(Guid timeId, int numero, Pessoa pessoa)
    {
        Validacoes.ValidarMinimoMaximo(numero, 0, 99, "Número de jogador deve estar entre 0 e 99");
        Validacoes.ValidarSeNulo(pessoa, "Dados da pessoa não podem ser nulos");

        TimeId = timeId;
        Numero = numero;
        Pessoa = pessoa;
        Ativo = true;
    }

    public void Desativar() => Ativo = false;
    public void Ativar() => Ativo = true;
}

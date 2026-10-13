using FluentAssertions;
using SisMaster.WebApps.WebApi.Application.Services;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

/// <summary>A hora do sistema é a da Bahia (UTC−3), não a do servidor (UTC no Docker): depois das 21h em Salvador o dia não vira antes da hora.</summary>
public class RelogioLocalTests
{
    [Fact]
    public void Agora_e_a_hora_da_bahia_tres_horas_atras_do_utc()
    {
        var utc = DateTime.UtcNow;
        var local = RelogioLocal.Agora;

        (utc - local).TotalMinutes.Should().BeInRange(179, 181);
    }

    [Fact]
    public void Hoje_acompanha_a_data_da_bahia()
    {
        RelogioLocal.Hoje.Should().Be(DateOnly.FromDateTime(RelogioLocal.Agora));
    }
}

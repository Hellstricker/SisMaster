using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Email;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// E-mails sobre a situação de uma inscrição (aprovada, recusada, taxa confirmada). Quem se inscreveu pelo site é sempre avisado; nas
/// fichas feitas pela diretoria só se a diretoria marcar "avisar por e-mail" (o e-mail do cadastro pode estar desatualizado). Falha de
/// envio nunca desfaz a decisão da diretoria: fica no log.
/// </summary>
public class AvisosDeInscricao(IEnviadorDeEmail email, ILogger<AvisosDeInscricao> log)
{
    private const string Rodape = "\n\nVocê pode acompanhar tudo em Minhas inscrições, no site da associação.";

    public Task AprovadaAsync(Inscricao ficha, InscricaoCategoria pedido, bool taxaJaPaga, bool avisar, CancellationToken ct) =>
        Enviar(ficha, avisar, $"Inscrição aprovada — {Referencia(ficha)}",
            taxaJaPaga
                ? $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi aprovado e já está efetivado: a taxa da sua ficha já estava paga."
                : $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi aprovado. Falta pagar a taxa de inscrição para efetivá-lo: a associação envia as instruções de pagamento e confirma o pagamento por aqui.",
            ct);

    public Task RecusadaAsync(Inscricao ficha, InscricaoCategoria pedido, bool avisar, CancellationToken ct) =>
        Enviar(ficha, avisar, $"Inscrição recusada — {Referencia(ficha)}",
            $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi recusado.\n\nMotivo: {pedido.MotivoRecusa}\n\nSe quiser, faça uma nova inscrição escolhendo outra categoria. Nenhum valor foi cobrado.",
            ct);

    public Task TaxaConfirmadaAsync(Inscricao ficha, bool avisar, CancellationToken ct)
    {
        var efetivadas = ficha.Categorias.Where(c => c.Status == StatusInscricaoCategoria.Efetivada).Select(c => c.TemporadaCategoria.Nome).ToList();
        return Enviar(ficha, avisar, $"Pagamento confirmado — {Referencia(ficha)}",
            efetivadas.Count > 0
                ? $"Recebemos o pagamento da taxa de inscrição. Estão efetivados: {string.Join(", ", efetivadas)}."
                : "Recebemos o pagamento da taxa de inscrição.",
            ct);
    }

    /// <summary>"#2027-0142" nas fichas do site; o ano e o campeonato nas feitas pela diretoria (que não têm protocolo).</summary>
    private static string Referencia(Inscricao ficha) =>
        ficha.Protocolo is not null ? $"#{ficha.Protocolo}"
        : ficha.Categorias.Select(c => c.TemporadaCategoria.Temporada).FirstOrDefault() is { } t ? $"inscrição de {t.Ano}" : "sua inscrição";

    private async Task Enviar(Inscricao ficha, bool avisar, string assunto, string texto, CancellationToken ct)
    {
        if (!ficha.VeioDoSite && !avisar) return;
        try
        {
            await email.EnviarAsync(ficha.Pessoa.Email, assunto, $"Olá, {ficha.Pessoa.Nome.Split(' ')[0]}!\n\n{texto}{Rodape}", ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha ao enviar o aviso \"{Assunto}\" (ficha {Ficha})", assunto, ficha.Id);
        }
    }
}

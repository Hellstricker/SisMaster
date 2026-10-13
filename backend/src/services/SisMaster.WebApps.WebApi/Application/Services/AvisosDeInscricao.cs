using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Email;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// E-mails sobre a situação de uma inscrição (aprovada, recusada, taxa confirmada). Só quem se inscreveu pelo site é avisado: as fichas
/// feitas pela diretoria são tratadas com a própria pessoa. Falha de envio nunca desfaz a decisão da diretoria: fica no log.
/// </summary>
public class AvisosDeInscricao(IEnviadorDeEmail email, ILogger<AvisosDeInscricao> log)
{
    private const string Rodape = "\n\nVocê pode acompanhar tudo em Minhas inscrições, no site da associação.";

    public Task AprovadaAsync(Inscricao ficha, InscricaoCategoria pedido, bool taxaJaPaga, CancellationToken ct) =>
        Enviar(ficha, $"Inscrição aprovada — #{ficha.Protocolo}",
            taxaJaPaga
                ? $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi aprovado e já está efetivado: a taxa da sua ficha já estava paga."
                : $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi aprovado. Falta pagar a taxa de inscrição para efetivá-lo: a associação envia as instruções de pagamento e confirma o pagamento por aqui.",
            ct);

    public Task RecusadaAsync(Inscricao ficha, InscricaoCategoria pedido, CancellationToken ct) =>
        Enviar(ficha, $"Inscrição recusada — #{ficha.Protocolo}",
            $"O seu pedido em {pedido.TemporadaCategoria.Nome} foi recusado.\n\nMotivo: {pedido.MotivoRecusa}\n\nSe quiser, faça uma nova inscrição escolhendo outra categoria. Nenhum valor foi cobrado.",
            ct);

    public Task TaxaConfirmadaAsync(Inscricao ficha, CancellationToken ct)
    {
        var efetivadas = ficha.Categorias.Where(c => c.Status == StatusInscricaoCategoria.Efetivada).Select(c => c.TemporadaCategoria.Nome).ToList();
        return Enviar(ficha, $"Pagamento confirmado — #{ficha.Protocolo}",
            efetivadas.Count > 0
                ? $"Recebemos o pagamento da taxa de inscrição. Estão efetivados: {string.Join(", ", efetivadas)}."
                : "Recebemos o pagamento da taxa de inscrição.",
            ct);
    }

    private async Task Enviar(Inscricao ficha, string assunto, string texto, CancellationToken ct)
    {
        if (ficha.Origem != "Site") return;
        try
        {
            await email.EnviarAsync(ficha.Pessoa.Email, assunto, $"Olá, {ficha.Pessoa.Nome.Split(' ')[0]}!\n\n{texto}{Rodape}", ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha ao enviar o aviso \"{Assunto}\" (protocolo {Protocolo})", assunto, ficha.Protocolo);
        }
    }
}

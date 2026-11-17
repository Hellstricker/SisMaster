using Microsoft.EntityFrameworkCore;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Domain.Torneio;
using SisMaster.WebApps.WebApi.Email;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// O convite do atleta para a equipe. Quem tem o link vê só o mínimo (equipe, torneio, primeiro nome e e-mail mascarado); para confirmar, o atleta entra
/// no site com a conta do e-mail convidado, completa a ficha e dá as duas autorizações. A confirmação cria a ficha de inscrição dele (origem "Equipe").
/// </summary>
public class ConviteDeAtletaServico
{
    private readonly IInscricaoDeEquipeRepository _equipes;
    private readonly IInscricaoRepository _inscricoes;
    private readonly ITemporadaRepository _temporadas;
    private readonly InscricaoDoParticipanteServico _participante;
    private readonly IEnviadorDeEmail _email;
    private readonly ILogger<ConviteDeAtletaServico> _log;

    public ConviteDeAtletaServico(IInscricaoDeEquipeRepository equipes, IInscricaoRepository inscricoes, ITemporadaRepository temporadas,
        InscricaoDoParticipanteServico participante, IEnviadorDeEmail email, ILogger<ConviteDeAtletaServico> log)
    {
        _equipes = equipes;
        _inscricoes = inscricoes;
        _temporadas = temporadas;
        _participante = participante;
        _email = email;
        _log = log;
    }

    public static string MascararEmail(string email)
    {
        var partes = email.Split('@');
        if (partes.Length != 2 || partes[0].Length == 0) return "***";
        return $"{partes[0][0]}***@{partes[1]}";
    }

    private async Task<(InscricaoDeEquipe Inscricao, ConviteDeAtleta Convite)?> LocalizarAsync(string codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 100) return null;
        var inscricao = await _equipes.ObterPorCodigoDoConviteAsync(codigo, ct);
        var hash = InscricaoDeEquipe.Hash(codigo);
        var convite = inscricao?.Convites.FirstOrDefault(c => c.CodigoHash == hash);
        return inscricao is null || convite is null ? null : (inscricao, convite);
    }

    /// <summary>O que o link mostra, sem login. Código desconhecido: nulo.</summary>
    public async Task<object?> ConsultarAsync(string codigo, DateTime agora, CancellationToken ct)
    {
        var achado = await LocalizarAsync(codigo, ct);
        if (achado is null) return null;
        var (i, c) = achado.Value;
        var temporada = await _temporadas.ObterPorIdAsync(i.TemporadaId, ct);
        var categoria = temporada?.Categorias.FirstOrDefault(x => x.Id == i.TemporadaCategoriaId);
        var situacao = i.Status == StatusInscricaoDeEquipe.Recusada ? "EquipeRecusada"
            : c.Status == StatusDoConvite.Enviado && c.Vencido(DateTime.UtcNow) ? "Vencido"
            : c.Status.ToString();
        return new
        {
            Equipe = i.Nome,
            Responsavel = i.ResponsavelNome.Split(' ')[0],
            Campeonato = temporada?.Campeonato?.Nome,
            Ano = temporada?.Ano,
            Categoria = categoria?.Nome,
            Taxa = temporada?.TaxaInscricao,
            AssociacaoSigla = temporada?.Campeonato?.Associacao?.Sigla,
            TemporadaId = i.TemporadaId,
            PrimeiroNome = c.Nome.Split(' ')[0],
            EmailMascarado = MascararEmail(c.Email),
            Situacao = situacao,
            ValidoAte = RelogioLocal.DeUtc(c.ValidoAte)
        };
    }

    public async Task RecusarAsync(string codigo, CancellationToken ct)
    {
        var achado = await LocalizarAsync(codigo, ct) ?? throw new DomainException("Convite não encontrado");
        var (i, c) = achado;
        if (c.Status != StatusDoConvite.Enviado) throw new DomainException("Este convite já foi respondido");
        var nome = c.Nome.Split(' ')[0];
        c.Recusar(DateTime.UtcNow);
        await _equipes.UnitOfWork.Commit();
        await Avisar(i.ResponsavelEmail, i.ResponsavelNome, $"{nome} recusou o convite — {i.Nome}",
            $"{nome} recusou o convite para a equipe {i.Nome}. Você pode convidar outro atleta em Minhas inscrições.", ct);
    }

    public async Task<Inscricao> ConfirmarAsync(string contaId, string contaEmail, string codigo, string? telefone, FichaDoSite ficha, bool aceiteDados,
        bool aceiteImagem, string versaoTermos, DateTime agora, CancellationToken ct)
    {
        if (!aceiteDados || !aceiteImagem)
            throw new DomainException("As duas autorizações (dados pessoais e uso de imagem) são necessárias para participar");
        var achado = await LocalizarAsync(codigo, ct) ?? throw new DomainException("Convite não encontrado");
        var (equipe, convite) = achado;

        if (equipe.Status == StatusInscricaoDeEquipe.Recusada) throw new DomainException("A inscrição desta equipe não foi aprovada");
        if (convite.Status != StatusDoConvite.Enviado) throw new DomainException("Este convite já foi respondido");
        if (convite.Vencido(DateTime.UtcNow)) throw new DomainException("O convite venceu. Peça ao responsável da equipe para reenviar");
        if (!string.Equals(contaEmail.Trim(), convite.Email, StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Este convite é para o e-mail {MascararEmail(convite.Email)}. Entre no site com a conta desse e-mail");

        var temporada = await _temporadas.ObterPorIdAsync(equipe.TemporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        if (temporada.Status == StatusTemporada.Encerrada) throw new DomainException("A temporada está encerrada");
        var categoria = temporada.Categorias.First(c => c.Id == equipe.TemporadaCategoriaId);

        var hoje = DateOnly.FromDateTime(agora);
        var vinculo = await _participante.VincularAsync(contaId, contaEmail,
            new DadosDaPessoaDoSite(convite.Nome, convite.Cpf!, convite.Nascimento!.Value, convite.Sexo, telefone), hoje, ct);
        switch (vinculo.Situacao)
        {
            case SituacaoDoVinculo.Menor: throw new DomainException("A inscrição de menor de 18 anos é feita pela diretoria, com a autorização do responsável legal");
            case SituacaoDoVinculo.Bloqueada: throw new DomainException("Não foi possível ligar este convite à sua conta. Fale com o responsável da equipe ou com a diretoria");
        }
        var pessoa = vinculo.Pessoa!;
        // O convite vale para o CPF informado pelo responsável: uma conta já ligada a outro CPF não pode usá-lo.
        if (pessoa.Cpf.Numero != convite.Cpf)
            throw new DomainException("Este convite foi feito para outro CPF. Confira com o responsável da equipe");

        if (await _inscricoes.ExisteAtivaAsync(pessoa.Id, categoria.Id, ct))
            throw new DomainException($"Você já tem um pedido ativo na categoria {categoria.Nome}. Veja em Minhas inscrições");

        var inscricao = new Inscricao(pessoa.Id, temporada.Id, ficha.AlturaCm, ficha.PesoKg, ficha.Posicao, ficha.PossuiPlanoSaude, ficha.NomePlanoSaude, aceiteDados);
        var pedido = inscricao.AdicionarCategoria(categoria.Id);
        var numero = await _inscricoes.ContarProtocolosAsync(temporada.Id, ct) + 1;
        inscricao.RegistrarEnvioPeloSite($"{temporada.Ano}-{numero:D4}", DateTime.UtcNow, versaoTermos, Guid.NewGuid(), "Equipe");

        // Equipe já aprovada: o pedido do atleta que chega depois é aprovado junto, salvo se estiver fora do esperado (aí a diretoria decide).
        var aprovadoNaHora = false;
        if (equipe.Status == StatusInscricaoDeEquipe.Aprovada
            && categoria.DescreverExcecoes(pessoa.Nascimento, pessoa.Sexo, temporada.Ano).Count == 0)
        {
            inscricao.AprovarCategoria(pedido.Id, null, false);
            aprovadoNaHora = true;
        }

        convite.Confirmar(inscricao.Id, pessoa.Id, DateTime.UtcNow);
        _inscricoes.Adicionar(inscricao);
        try
        {
            await _inscricoes.UnitOfWork.Commit();
        }
        catch (DbUpdateException ex)
        {
            _log.LogWarning(ex, "Conflito ao confirmar o convite (protocolo repetido)");
            throw new DomainException("Não foi possível concluir agora. Tente de novo");
        }

        var taxa = temporada.TaxaInscricao is { } t ? $" A taxa de inscrição é de R$ {t:N2}." : string.Empty;
        await Avisar(pessoa.Email, pessoa.Nome, $"Participação confirmada — #{inscricao.Protocolo}",
            $"Você confirmou a participação na equipe {equipe.Nome} ({categoria.Nome}) no {temporada.Campeonato?.Nome} {temporada.Ano}.\n\nProtocolo: #{inscricao.Protocolo}\n\n" +
            (aprovadoNaHora
                ? $"A equipe já foi aprovada. Falta pagar a taxa de inscrição para efetivar a sua participação.{taxa}"
                : $"A diretoria vai analisar a equipe. Nada é cobrado agora.{taxa}"), ct);
        await Avisar(equipe.ResponsavelEmail, equipe.ResponsavelNome, $"{pessoa.Nome.Split(' ')[0]} confirmou a participação — {equipe.Nome}",
            $"{pessoa.Nome} confirmou a participação na equipe {equipe.Nome}. Confirmados: {equipe.Confirmados} de {equipe.Ativos.Count()} convidados.", ct);
        return inscricao;
    }

    private async Task Avisar(string email, string nome, string assunto, string texto, CancellationToken ct)
    {
        try
        {
            await _email.EnviarAsync(email, assunto, $"Olá, {nome.Split(' ')[0]}!\n\n{texto}", ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enviar o e-mail \"{Assunto}\"", assunto);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Domain.Torneio;
using SisMaster.WebApps.WebApi.Email;
using Cpf = SisMaster.WebApps.WebApi.Domain.Participantes.Cpf;

namespace SisMaster.WebApps.WebApi.Application.Services;

public sealed record AtletaConvidado(string Nome, string Cpf, DateOnly Nascimento, Sexo Sexo, string Email);

public sealed record DadosDaEquipe(Guid TemporadaId, Guid TemporadaCategoriaId, string Nome, string ResponsavelNome, string? ResponsavelTelefone,
    bool ResponsavelJoga, AtletaConvidado? Responsavel, IReadOnlyList<AtletaConvidado> Atletas, Guid CodigoDeEnvio);

/// <summary>
/// A inscrição de equipe em torneio aberto: o responsável envia o nome da equipe e convida os atletas (um e-mail com link para cada um), os atletas
/// confirmam o convite (ver <see cref="ConviteDeAtletaServico"/>) e a diretoria aprova a equipe. A taxa é por atleta: ao ser registrada, o atleta
/// efetivado entra no elenco da equipe criada na aprovação.
/// </summary>
public class InscricaoDeEquipeServico
{
    private readonly IInscricaoDeEquipeRepository _equipes;
    private readonly IInscricaoRepository _inscricoes;
    private readonly IPessoaRepository _pessoas;
    private readonly ITemporadaRepository _temporadas;
    private readonly IEquipeRepository _elencos;
    private readonly IEnviadorDeEmail _email;
    private readonly IAuditoria _auditoria;
    private readonly IConfiguration _config;
    private readonly ILogger<InscricaoDeEquipeServico> _log;

    public InscricaoDeEquipeServico(IInscricaoDeEquipeRepository equipes, IInscricaoRepository inscricoes, IPessoaRepository pessoas, ITemporadaRepository temporadas,
        IEquipeRepository elencos, IEnviadorDeEmail email, IAuditoria auditoria, IConfiguration config, ILogger<InscricaoDeEquipeServico> log)
    {
        _equipes = equipes;
        _inscricoes = inscricoes;
        _pessoas = pessoas;
        _temporadas = temporadas;
        _elencos = elencos;
        _email = email;
        _auditoria = auditoria;
        _config = config;
        _log = log;
    }

    public string LinkDoConvite(string codigo) => $"{(_config["App:UrlPublica"] ?? "http://localhost:3000").TrimEnd('/')}/convites/{codigo}";

    // ------------------------------------------------------------------ responsável

    public async Task<InscricaoDeEquipe> CriarAsync(string contaId, string contaEmail, DadosDaEquipe d, DateTime agora, CancellationToken ct)
    {
        var jaGravada = await _equipes.ObterPorCodigoDeEnvioAsync(d.CodigoDeEnvio, ct);
        if (jaGravada is not null)
        {
            if (jaGravada.ContaId != contaId) throw new DomainException("Código de envio inválido. Recarregue a página e tente de novo");
            return jaGravada;
        }

        var temporada = await _temporadas.ObterPorIdAsync(d.TemporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        if (!temporada.Aberta) throw new DomainException("Esta temporada não aceita inscrição de equipe");
        if (!InscricaoDoParticipanteServico.InscricoesAbertas(temporada, agora)) throw new DomainException("As inscrições desta temporada não estão abertas");
        var categoria = temporada.Categorias.FirstOrDefault(c => c.Id == d.TemporadaCategoriaId) ?? throw new DomainException("Categoria não pertence a esta temporada");

        var nome = (d.Nome ?? string.Empty).Trim();
        if (await _equipes.ExisteNomeAsync(temporada.Id, nome, ct) || await _elencos.ExisteNomeNaTemporadaAsync(temporada.Id, nome, null, ct))
            throw new DomainException("Já existe uma equipe com este nome nesta temporada. Escolha outro nome");

        var atletas = new List<AtletaConvidado>();
        if (d.ResponsavelJoga)
            atletas.Add(d.Responsavel ?? throw new DomainException("Informe os seus dados de atleta"));
        atletas.AddRange(d.Atletas);

        if (atletas.Count < InscricaoDeEquipe.MinimoDeAtletas)
            throw new DomainException($"A equipe precisa de pelo menos {InscricaoDeEquipe.MinimoDeAtletas} atletas");
        if (atletas.Count > InscricaoDeEquipe.MaximoDeAtletas)
            throw new DomainException($"A equipe pode ter no máximo {InscricaoDeEquipe.MaximoDeAtletas} atletas");

        var hoje = DateOnly.FromDateTime(agora);
        var cpfs = new List<string>();
        foreach (var a in atletas) cpfs.Add(await ValidarAtletaAsync(a, hoje, ct));
        if (cpfs.Distinct().Count() != cpfs.Count) throw new DomainException("Há CPF repetido na lista de atletas");
        if (atletas.Select(a => a.Email.Trim().ToLowerInvariant()).Distinct().Count() != atletas.Count)
            throw new DomainException("Há e-mail repetido na lista: cada atleta precisa do próprio e-mail");
        var emUso = await _equipes.CpfsJaConvidadosAsync(categoria.Id, cpfs, ct);
        if (emUso.Count > 0) throw new DomainException("Um dos atletas já está em outra equipe desta categoria");
        foreach (var cpf in cpfs)
            if (await _inscricoes.ExisteAtivaPorCpfAsync(cpf, categoria.Id, ct))
                throw new DomainException("Um dos atletas já tem inscrição ativa nesta categoria");

        var numero = await _equipes.ContarProtocolosAsync(temporada.Id, ct) + 1;
        var inscricao = new InscricaoDeEquipe(temporada.Id, categoria.Id, contaId, d.ResponsavelNome, contaEmail, d.ResponsavelTelefone, nome,
            $"E{temporada.Ano}-{numero:D4}", d.CodigoDeEnvio, DateTime.UtcNow);

        var envios = new List<(AtletaConvidado Atleta, string Codigo)>();
        for (var i = 0; i < atletas.Count; i++)
        {
            var a = atletas[i];
            var ehResponsavel = d.ResponsavelJoga && i == 0;
            // O responsável recebe o próprio convite no e-mail da conta; os demais, no e-mail informado.
            var email = ehResponsavel ? contaEmail : a.Email;
            var (_, codigo) = inscricao.Convidar(a.Nome, cpfs[i], a.Nascimento, a.Sexo, email, DateTime.UtcNow, ehResponsavel);
            envios.Add((a with { Email = email }, codigo));
        }

        _equipes.Adicionar(inscricao);
        try
        {
            await _equipes.UnitOfWork.Commit();
        }
        catch (DbUpdateException ex)
        {
            _log.LogWarning(ex, "Conflito ao gravar a inscrição de equipe (protocolo ou código de envio repetido)");
            throw new DomainException("Não foi possível concluir o envio agora. Confira em Minhas inscrições e, se não aparecer, tente de novo");
        }

        foreach (var (a, codigo) in envios) await EnviarConviteAsync(inscricao, temporada, a.Nome, a.Email, codigo, ct);
        await AvisarResponsavelAsync(inscricao, $"Inscrição da equipe recebida — #{inscricao.Protocolo}",
            $"Recebemos a inscrição da equipe {inscricao.Nome} ({categoria.Nome}) no {temporada.Campeonato?.Nome} {temporada.Ano}.\n\nProtocolo: #{inscricao.Protocolo}\n\n" +
            $"Cada atleta recebeu um e-mail com o link para confirmar a participação (vale por {InscricaoDeEquipe.DiasParaConfirmar} dias). Quando pelo menos {InscricaoDeEquipe.MinimoDeAtletas} confirmarem, a diretoria analisa a equipe.", ct);
        return inscricao;
    }

    /// <summary>Valida o atleta convidado (CPF real, maior de idade) e devolve o CPF só com dígitos.</summary>
    private async Task<string> ValidarAtletaAsync(AtletaConvidado a, DateOnly hoje, CancellationToken ct)
    {
        var cpf = Cpf.RemoverFormatacao(a.Cpf ?? string.Empty);
        var primeiro = (a.Nome ?? string.Empty).Trim().Split(' ')[0];
        if (!InscricaoDoParticipanteServico.CpfValido(cpf)) throw new DomainException($"CPF inválido para {primeiro}. Confira os 11 números");
        if (Pessoa.EhMenorDeIdade(a.Nascimento, hoje))
            throw new DomainException($"{primeiro} é menor de 18 anos: a inscrição de menor é feita pela diretoria, com a autorização do responsável legal");
        if (string.IsNullOrWhiteSpace(a.Email) || !a.Email.Contains('@')) throw new DomainException($"Informe um e-mail válido para {primeiro}");
        await Task.CompletedTask;
        return cpf;
    }

    public async Task<InscricaoDeEquipe> AdicionarAtletaAsync(string contaId, Guid id, AtletaConvidado a, DateTime agora, CancellationToken ct)
    {
        var inscricao = await ObterDaContaAsync(contaId, id, ct);
        var temporada = await _temporadas.ObterPorIdAsync(inscricao.TemporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        if (temporada.Status == StatusTemporada.Encerrada) throw new DomainException("A temporada está encerrada");
        var cpf = await ValidarAtletaAsync(a, DateOnly.FromDateTime(agora), ct);
        if ((await _equipes.CpfsJaConvidadosAsync(inscricao.TemporadaCategoriaId, [cpf], ct)).Count > 0)
            throw new DomainException("Este atleta já está em uma equipe desta categoria");
        if (await _inscricoes.ExisteAtivaPorCpfAsync(cpf, inscricao.TemporadaCategoriaId, ct))
            throw new DomainException("Este atleta já tem inscrição ativa nesta categoria");

        var (convite, codigo) = inscricao.Convidar(a.Nome, cpf, a.Nascimento, a.Sexo, a.Email, DateTime.UtcNow);
        _equipes.AdicionarConvite(convite);
        await _equipes.UnitOfWork.Commit();
        await EnviarConviteAsync(inscricao, temporada, a.Nome, a.Email, codigo, ct);
        return inscricao;
    }

    public async Task ReenviarAsync(string contaId, Guid id, Guid conviteId, DateTime agora, CancellationToken ct)
    {
        var inscricao = await ObterDaContaAsync(contaId, id, ct);
        var temporada = await _temporadas.ObterPorIdAsync(inscricao.TemporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        var codigo = inscricao.Reenviar(conviteId, DateTime.UtcNow);
        await _equipes.UnitOfWork.Commit();
        var c = inscricao.Obter(conviteId);
        await EnviarConviteAsync(inscricao, temporada, c.Nome, c.Email, codigo, ct);
    }

    public async Task RemoverAsync(string contaId, Guid id, Guid conviteId, CancellationToken ct)
    {
        var inscricao = await ObterDaContaAsync(contaId, id, ct);
        inscricao.Remover(conviteId);
        await _equipes.UnitOfWork.Commit();
    }

    private async Task<InscricaoDeEquipe> ObterDaContaAsync(string contaId, Guid id, CancellationToken ct)
    {
        var i = await _equipes.ObterPorIdAsync(id, ct);
        // Inscrição de outra conta: mesma resposta de "não existe".
        if (i is null || i.ContaId != contaId) throw new DomainException(NaoEncontrada);
        return i;
    }

    public const string NaoEncontrada = "Inscrição de equipe não encontrada";

    /// <summary>As fichas dos atletas que confirmaram o convite desta equipe (para a diretoria registrar a taxa em lote só de quem é da equipe).</summary>
    public async Task<IReadOnlySet<Guid>> FichasDaEquipeAsync(Guid id, CancellationToken ct)
    {
        var i = await _equipes.ObterPorIdAsync(id, ct);
        return i is null ? new HashSet<Guid>() : i.Convites.Where(c => c.Status == StatusDoConvite.Confirmado && c.InscricaoId is not null).Select(c => c.InscricaoId!.Value).ToHashSet();
    }

    // ------------------------------------------------------------------ consultas do responsável

    public async Task<object> ListarMinhasAsync(string contaId, CancellationToken ct)
    {
        var lista = await _equipes.ListarDaContaAsync(contaId, ct);
        var temporadas = new Dictionary<Guid, Temporada?>();
        foreach (var t in lista.Select(i => i.TemporadaId).Distinct()) temporadas[t] = await _temporadas.ObterPorIdAsync(t, ct);
        return new
        {
            Equipes = lista.Select(i =>
            {
                var t = temporadas[i.TemporadaId];
                return new
                {
                    i.Id, i.Protocolo, i.Nome,
                    Campeonato = t?.Campeonato?.Nome, Ano = t?.Ano, i.TemporadaId,
                    Categoria = t?.Categorias.FirstOrDefault(c => c.Id == i.TemporadaCategoriaId)?.Nome,
                    Situacao = Situacao(i), EnviadaEm = RelogioLocal.DeUtc(i.DataEnvio),
                    Confirmados = i.Confirmados, Convidados = i.Ativos.Count()
                };
            }).ToList()
        };
    }

    public static string Situacao(InscricaoDeEquipe i) => i.Status switch
    {
        StatusInscricaoDeEquipe.Aprovada => "Aprovada",
        StatusInscricaoDeEquipe.Recusada => "Recusada",
        _ => i.AguardandoConfirmacoes ? "AguardandoConfirmacoes" : "EmAnalise"
    };

    public async Task<object?> DetalharMinhaAsync(string contaId, Guid id, CancellationToken ct)
    {
        var i = await _equipes.ObterPorIdAsync(id, ct);
        if (i is null || i.ContaId != contaId) return null;
        return await DetalharAsync(i, paraResponsavel: true, ct);
    }

    /// <summary>O detalhe de uma inscrição de equipe, com a situação da taxa de cada atleta. O responsável só vê o primeiro nome, a situação e o e-mail de quem convidou.</summary>
    public async Task<object> DetalharAsync(InscricaoDeEquipe i, bool paraResponsavel, CancellationToken ct)
    {
        var temporada = await _temporadas.ObterPorIdAsync(i.TemporadaId, ct);
        var fichas = new Dictionary<Guid, Inscricao?>();
        foreach (var c in i.Convites.Where(c => c.InscricaoId is not null)) fichas[c.InscricaoId!.Value] = await _inscricoes.ObterPorIdAsync(c.InscricaoId!.Value, ct);

        object Atleta(ConviteDeAtleta c)
        {
            var ficha = c.InscricaoId is { } fid && fichas.TryGetValue(fid, out var f) ? f : null;
            var pedido = ficha?.Categorias.FirstOrDefault(p => p.TemporadaCategoriaId == i.TemporadaCategoriaId);
            return new
            {
                c.Id, Nome = paraResponsavel ? c.Nome : c.Nome, c.Email, c.EhResponsavel,
                Cpf = paraResponsavel || c.Cpf is null ? null : InscricaoDoParticipanteServico.Mascarar(c.Cpf),
                Nascimento = paraResponsavel ? null : c.Nascimento?.ToString("yyyy-MM-dd"),
                Status = c.Status == StatusDoConvite.Enviado && c.Vencido(DateTime.UtcNow) ? "Vencido" : c.Status.ToString(),
                ValidoAte = RelogioLocal.DeUtc(c.ValidoAte),
                RespondidoEm = RelogioLocal.DeUtc(c.RespondidoEm),
                FichaId = paraResponsavel ? (Guid?)null : c.InscricaoId,
                PedidoId = paraResponsavel ? (Guid?)null : pedido?.Id,
                Pedido = pedido?.Status.ToString(),
                TaxaPaga = ficha?.TaxaPaga ?? false,
                NaEquipe = pedido?.Status == StatusInscricaoCategoria.Efetivada && i.Status == StatusInscricaoDeEquipe.Aprovada
            };
        }

        return new
        {
            i.Id, i.Protocolo, i.Nome, i.TemporadaId, i.TemporadaCategoriaId,
            Campeonato = temporada?.Campeonato?.Nome, Ano = temporada?.Ano,
            Categoria = temporada?.Categorias.FirstOrDefault(c => c.Id == i.TemporadaCategoriaId)?.Nome,
            Taxa = temporada?.TaxaInscricao,
            Situacao = Situacao(i), i.MotivoRecusa, i.EquipeId, i.JustificativaExcecao,
            Responsavel = new { Nome = i.ResponsavelNome, Email = i.ResponsavelEmail, Telefone = i.ResponsavelTelefone },
            EnviadaEm = RelogioLocal.DeUtc(i.DataEnvio), DecididaEm = RelogioLocal.DeUtc(i.DecididaEm),
            Minimo = InscricaoDeEquipe.MinimoDeAtletas, Maximo = InscricaoDeEquipe.MaximoDeAtletas,
            Confirmados = i.Confirmados, EmAberto = i.ConvitesEmAberto,
            Atletas = i.Convites.Where(c => c.Status != StatusDoConvite.Removido).OrderBy(c => c.EhResponsavel ? 0 : 1).ThenBy(c => c.Nome).Select(Atleta).ToList()
        };
    }

    // ------------------------------------------------------------------ diretoria

    public async Task<object> ListarDaTemporadaAsync(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await _temporadas.ObterPorIdAsync(temporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        var lista = await _equipes.ListarDaTemporadaAsync(temporadaId, ct);
        return new
        {
            Temporada = new { temporada.Id, Campeonato = temporada.Campeonato?.Nome, temporada.Ano, temporada.TaxaInscricao, Aberta = temporada.Aberta },
            Equipes = lista.Select(i => new
            {
                i.Id, i.Protocolo, i.Nome,
                Categoria = temporada.Categorias.FirstOrDefault(c => c.Id == i.TemporadaCategoriaId)?.Nome,
                Responsavel = i.ResponsavelNome, ResponsavelEmail = i.ResponsavelEmail, ResponsavelTelefone = i.ResponsavelTelefone,
                Situacao = Situacao(i), EnviadaEm = RelogioLocal.DeUtc(i.DataEnvio),
                Confirmados = i.Confirmados, EmAberto = i.ConvitesEmAberto, i.EquipeId
            }).ToList()
        };
    }

    public async Task<object?> DetalharParaDiretoriaAsync(Guid id, CancellationToken ct)
    {
        var i = await _equipes.ObterPorIdAsync(id, ct);
        return i is null ? null : await DetalharAsync(i, paraResponsavel: false, ct);
    }

    /// <summary>Aprova a equipe: cria a Equipe (elenco vazio) e aprova os pedidos dos atletas já confirmados. Cada atleta entra no elenco ao ter a taxa registrada.</summary>
    public async Task AprovarAsync(Guid id, string? justificativaExcecao, string? usuario, CancellationToken ct)
    {
        var inscricao = await _equipes.ObterPorIdAsync(id, ct) ?? throw new DomainException("Inscrição de equipe não encontrada");
        var temporada = await _temporadas.ObterPorIdAsync(inscricao.TemporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        if (inscricao.Status != StatusInscricaoDeEquipe.Pendente) throw new DomainException("Só é possível aprovar uma equipe pendente");
        if (inscricao.Confirmados < InscricaoDeEquipe.MinimoDeAtletas)
            throw new DomainException($"A equipe precisa de pelo menos {InscricaoDeEquipe.MinimoDeAtletas} atletas confirmados para ser aprovada");
        if (await _elencos.ExisteNomeNaTemporadaAsync(inscricao.TemporadaId, inscricao.Nome, null, ct))
            throw new DomainException("Já existe uma equipe com este nome na temporada");

        var categoria = temporada.Categorias.First(c => c.Id == inscricao.TemporadaCategoriaId);
        var aprovacoes = new List<(Inscricao Ficha, InscricaoCategoria Pedido)>();
        var comExcecao = new List<string>();
        foreach (var c in inscricao.Convites.Where(c => c.Status == StatusDoConvite.Confirmado && c.InscricaoId is not null))
        {
            var ficha = await _inscricoes.ObterPorIdAsync(c.InscricaoId!.Value, ct);
            var pedido = ficha?.Categorias.FirstOrDefault(p => p.TemporadaCategoriaId == categoria.Id && p.Status == StatusInscricaoCategoria.Pendente);
            if (ficha is null || pedido is null) continue;
            var motivos = categoria.DescreverExcecoes(ficha.Pessoa.Nascimento, ficha.Pessoa.Sexo, temporada.Ano);
            if (motivos.Count > 0) comExcecao.Add($"{ficha.Pessoa.Nome}: {string.Join("; ", motivos)}");
            aprovacoes.Add((ficha, pedido));
        }
        if (comExcecao.Count > 0 && string.IsNullOrWhiteSpace(justificativaExcecao))
            throw new DomainException($"Há atleta fora do esperado para a categoria ({string.Join(" | ", comExcecao)}). Informe a justificativa para aprovar mesmo assim");

        var equipe = new Equipe(categoria.Id, inscricao.Nome, null);
        _elencos.Adicionar(equipe);
        foreach (var (ficha, pedido) in aprovacoes)
        {
            var fora = categoria.DescreverExcecoes(ficha.Pessoa.Nascimento, ficha.Pessoa.Sexo, temporada.Ano).Count > 0;
            ficha.AprovarCategoria(pedido.Id, fora ? justificativaExcecao : null, fora);
        }
        inscricao.Aprovar(equipe.Id, justificativaExcecao, DateTime.UtcNow);
        await _equipes.UnitOfWork.Commit();
        _auditoria.Registrar("AprovacaoDeEquipe", "InscricaoDeEquipe", inscricao.Id, $"Equipe {inscricao.Nome} aprovada com {aprovacoes.Count} atletas ({usuario})");

        await AvisarResponsavelAsync(inscricao, $"Equipe aprovada — #{inscricao.Protocolo}",
            $"A equipe {inscricao.Nome} foi aprovada no {temporada.Campeonato?.Nome} {temporada.Ano}!\n\nCada atleta entra no elenco quando a taxa de inscrição dele é registrada pela associação.", ct);
        foreach (var (ficha, pedido) in aprovacoes)
            await TentarAvisarAtletaAsync(ficha.Pessoa.Email, ficha.Pessoa.Nome, $"Inscrição aprovada — #{ficha.Protocolo}",
                $"A equipe {inscricao.Nome} foi aprovada e o seu pedido em {categoria.Nome} também. Falta pagar a taxa de inscrição para efetivar a sua participação: a associação confirma o pagamento por aqui.", ct);
    }

    public async Task RecusarAsync(Guid id, string motivo, string? usuario, CancellationToken ct)
    {
        var inscricao = await _equipes.ObterPorIdAsync(id, ct) ?? throw new DomainException("Inscrição de equipe não encontrada");
        inscricao.Recusar(motivo, DateTime.UtcNow);
        var recusados = new List<Inscricao>();
        foreach (var c in inscricao.Convites.Where(c => c.Status == StatusDoConvite.Confirmado && c.InscricaoId is not null))
        {
            var ficha = await _inscricoes.ObterPorIdAsync(c.InscricaoId!.Value, ct);
            foreach (var p in ficha?.Categorias.Where(p => p.TemporadaCategoriaId == inscricao.TemporadaCategoriaId && p.Status == StatusInscricaoCategoria.Pendente) ?? [])
                p.Recusar($"Equipe recusada: {motivo.Trim()}");
            if (ficha is not null) recusados.Add(ficha);
        }
        await _equipes.UnitOfWork.Commit();
        _auditoria.Registrar("RecusaDeEquipe", "InscricaoDeEquipe", inscricao.Id, $"Equipe {inscricao.Nome} recusada: {motivo.Trim()} ({usuario})");

        await AvisarResponsavelAsync(inscricao, $"Equipe não aprovada — #{inscricao.Protocolo}",
            $"A inscrição da equipe {inscricao.Nome} não foi aprovada.\n\nMotivo: {inscricao.MotivoRecusa}\n\nNenhum valor foi cobrado.", ct);
        foreach (var ficha in recusados)
            await TentarAvisarAtletaAsync(ficha.Pessoa.Email, ficha.Pessoa.Nome, $"Inscrição não aprovada — #{ficha.Protocolo}",
                $"A equipe {inscricao.Nome} não foi aprovada e o seu pedido ficou sem efeito.\n\nMotivo: {inscricao.MotivoRecusa}\n\nNenhum valor foi cobrado.", ct);
    }

    // ------------------------------------------------------------------ elenco (taxa registrada)

    /// <summary>
    /// Coloca no elenco da equipe aprovada os pedidos efetivados da ficha (atleta que veio por convite). Não grava: quem chama faz o Commit.
    /// Ficha que não veio de equipe, equipe ainda não aprovada ou atleta já no elenco: não faz nada.
    /// </summary>
    public async Task ColocarNaEquipeAsync(Inscricao ficha, CancellationToken ct)
    {
        if (ficha.Origem != "Equipe") return;
        var inscricao = await _equipes.ObterPorFichaAsync(ficha.Id, ct);
        if (inscricao is not { Status: StatusInscricaoDeEquipe.Aprovada, EquipeId: { } equipeId }) return;

        var pedido = ficha.Categorias.FirstOrDefault(p => p.TemporadaCategoriaId == inscricao.TemporadaCategoriaId && p.Status == StatusInscricaoCategoria.Efetivada);
        if (pedido is null) return;
        if (await _elencos.AtletaExisteParaAsync(pedido.Id, ct)) return;

        var equipe = await _elencos.ObterPorIdAsync(equipeId, ct);
        if (equipe is null) return;
        if (equipe.Atletas.Count >= InscricaoDeEquipe.MaximoDeAtletas) throw new DomainException($"O elenco da equipe {equipe.Nome} já tem {InscricaoDeEquipe.MaximoDeAtletas} atletas");
        _elencos.AdicionarAtleta(equipe.AdicionarAtleta(pedido.Id));
    }

    // ------------------------------------------------------------------ e-mails

    private async Task EnviarConviteAsync(InscricaoDeEquipe i, Temporada t, string nome, string email, string codigo, CancellationToken ct) =>
        await TentarAvisarAtletaAsync(email, nome, $"Convite para a equipe {i.Nome}",
            $"{i.ResponsavelNome} convidou você para jogar na equipe {i.Nome} no {t.Campeonato?.Nome} {t.Ano}.\n\n" +
            $"Para confirmar a sua participação (e aceitar os termos), acesse:\n{LinkDoConvite(codigo)}\n\n" +
            $"O link é pessoal e vale por {InscricaoDeEquipe.DiasParaConfirmar} dias. Se você não conhece esta equipe, ignore este e-mail.", ct);

    private async Task AvisarResponsavelAsync(InscricaoDeEquipe i, string assunto, string texto, CancellationToken ct) =>
        await TentarAvisarAtletaAsync(i.ResponsavelEmail, i.ResponsavelNome, assunto, texto, ct);

    private async Task TentarAvisarAtletaAsync(string email, string nome, string assunto, string texto, CancellationToken ct)
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

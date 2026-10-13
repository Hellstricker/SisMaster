using Microsoft.EntityFrameworkCore;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Email;
using Cpf = SisMaster.WebApps.WebApi.Domain.Participantes.Cpf;

namespace SisMaster.WebApps.WebApi.Application.Services;

public sealed record DadosDaPessoaDoSite(string Nome, string Cpf, DateOnly Nascimento, Sexo Sexo, string? Telefone);

public enum SituacaoDoVinculo { Vinculada, Bloqueada, Menor }

public sealed record ResultadoDoVinculo(SituacaoDoVinculo Situacao, Pessoa? Pessoa);

public sealed record FichaDoSite(int? AlturaCm, int? PesoKg, string? Posicao, bool PossuiPlanoSaude, string? NomePlanoSaude);

/// <summary>
/// A inscrição pela conta do site (I2 e I3): liga a conta ao cadastro da Pessoa pelo CPF (sem nunca sobrescrever dados do cadastro e sem
/// revelar nada de quem não é dono dele), prepara o formulário e grava a inscrição com protocolo, as duas autorizações e idempotência.
/// O servidor revalida tudo; a tela só ajuda.
/// </summary>
public class InscricaoDoParticipanteServico
{
    private readonly IPessoaRepository _pessoas;
    private readonly IInscricaoRepository _inscricoes;
    private readonly ITemporadaRepository _temporadas;
    private readonly IEnviadorDeEmail _email;
    private readonly ILogger<InscricaoDoParticipanteServico> _log;

    public InscricaoDoParticipanteServico(IPessoaRepository pessoas, IInscricaoRepository inscricoes, ITemporadaRepository temporadas,
        IEnviadorDeEmail email, ILogger<InscricaoDoParticipanteServico> log)
    {
        _pessoas = pessoas;
        _inscricoes = inscricoes;
        _temporadas = temporadas;
        _email = email;
        _log = log;
    }

    // ------------------------------------------------------------------ CPF

    /// <summary>Confere os dígitos verificadores (o cadastro da diretoria só exige 11 dígitos; o site, que é aberto, exige um CPF real).</summary>
    public static bool CpfValido(string cpfSoDigitos)
    {
        if (cpfSoDigitos.Length != 11 || cpfSoDigitos.Distinct().Count() == 1 || !cpfSoDigitos.All(char.IsDigit)) return false;
        static int Dv(string s, int pesoInicial) { var soma = 0; for (var i = 0; i < s.Length; i++) soma += (s[i] - '0') * (pesoInicial - i); var r = soma * 10 % 11; return r == 10 ? 0 : r; }
        return Dv(cpfSoDigitos[..9], 10) == cpfSoDigitos[9] - '0' && Dv(cpfSoDigitos[..10], 11) == cpfSoDigitos[10] - '0';
    }

    public static string Mascarar(string cpfSoDigitos) => $"***.{cpfSoDigitos[3..6]}.{cpfSoDigitos[6..9]}-**";

    // ------------------------------------------------------------------ vínculo (I2)

    /// <summary>
    /// Liga a conta a um cadastro. Conta já ligada: devolve o cadastro (só o telefone pode ser atualizado). CPF novo: cria o cadastro ligado.
    /// CPF existente e sem conta com o MESMO e-mail da conta (confirmado): liga. Qualquer outro caso: bloqueia sem dizer nada do cadastro.
    /// </summary>
    public async Task<ResultadoDoVinculo> VincularAsync(string contaId, string contaEmail, DadosDaPessoaDoSite d, DateOnly hoje, CancellationToken ct)
    {
        var propria = await _pessoas.ObterPorContaAsync(contaId, ct);
        if (propria is not null)
        {
            if (!string.IsNullOrWhiteSpace(d.Telefone) && d.Telefone.Trim() != propria.Telefone)
            {
                propria.AtualizarContato(propria.Email, d.Telefone);
                await _pessoas.UnitOfWork.Commit();
            }
            return new ResultadoDoVinculo(Pessoa.EhMenorDeIdade(propria.Nascimento, hoje) ? SituacaoDoVinculo.Menor : SituacaoDoVinculo.Vinculada, propria);
        }

        var cpf = Cpf.RemoverFormatacao(d.Cpf);
        if (!CpfValido(cpf)) throw new DomainException("CPF inválido. Confira os 11 números.");
        if (Pessoa.EhMenorDeIdade(d.Nascimento, hoje)) return new ResultadoDoVinculo(SituacaoDoVinculo.Menor, null);

        var existente = await _pessoas.ObterPorCpfAsync(cpf, ct);
        if (existente is null)
        {
            var nova = new Pessoa(d.Nome, cpf, d.Nascimento, d.Sexo, contaEmail, d.Telefone);
            nova.VincularConta(contaId);
            _pessoas.Adicionar(nova);
            await _pessoas.UnitOfWork.Commit();
            return new ResultadoDoVinculo(SituacaoDoVinculo.Vinculada, nova);
        }

        if (existente.ContaId is null && string.Equals(existente.Email, contaEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (Pessoa.EhMenorDeIdade(existente.Nascimento, hoje)) return new ResultadoDoVinculo(SituacaoDoVinculo.Menor, null);
            existente.VincularConta(contaId);
            if (!string.IsNullOrWhiteSpace(d.Telefone) && d.Telefone.Trim() != existente.Telefone) existente.AtualizarContato(existente.Email, d.Telefone);
            await _pessoas.UnitOfWork.Commit();
            return new ResultadoDoVinculo(SituacaoDoVinculo.Vinculada, existente);
        }

        return new ResultadoDoVinculo(SituacaoDoVinculo.Bloqueada, null);
    }

    // ------------------------------------------------------------------ preparo do formulário

    public static bool InscricoesAbertas(Temporada t, DateTime agora) =>
        t.Status == StatusTemporada.InscricoesAbertas && t.InscricoesHabilitadas
        && t.DataInicioInscricoes.Date <= agora.Date && t.DataFimInscricoes.Date >= agora.Date;

    public async Task<object?> PrepararAsync(string contaId, Guid temporadaId, DateTime agora, CancellationToken ct)
    {
        var temporada = await _temporadas.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return null;

        var pessoa = await _pessoas.ObterPorContaAsync(contaId, ct);
        var hoje = DateOnly.FromDateTime(agora);
        var ativas = pessoa is null ? new Dictionary<Guid, string>() : await _inscricoes.ListarCategoriasAtivasDaPessoaAsync(pessoa.Id, temporada.Id, ct);
        var ultima = pessoa is null ? null : await _inscricoes.ObterUltimaDaPessoaAsync(pessoa.Id, ct);

        return new
        {
            Temporada = new
            {
                temporada.Id,
                Campeonato = temporada.Campeonato?.Nome,
                temporada.Ano,
                Abertas = InscricoesAbertas(temporada, agora),
                temporada.DataInicioInscricoes,
                temporada.DataFimInscricoes
            },
            Pessoa = pessoa is null ? null : new
            {
                pessoa.Nome,
                CpfMascarado = Mascarar(pessoa.Cpf.Numero),
                Nascimento = pessoa.Nascimento.ToString("yyyy-MM-dd"),
                Sexo = pessoa.Sexo.ToString(),
                pessoa.Telefone,
                Email = pessoa.Email,
                Menor = Pessoa.EhMenorDeIdade(pessoa.Nascimento, hoje)
            },
            UltimaFicha = ultima is null ? null : new { ultima.AlturaCm, ultima.PesoKg, ultima.Posicao, ultima.PossuiPlanoSaude, ultima.NomePlanoSaude },
            Categorias = temporada.Categorias.OrderBy(c => c.Nome).Select(c => new
            {
                c.Id,
                c.Nome,
                c.IdadeMinima,
                Sexo = c.Sexo?.ToString(),
                c.AceitaAbaixoIdadeMinima,
                JaPedida = ativas.TryGetValue(c.Id, out var protocolo) ? protocolo : null
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ envio (I3)

    /// <summary>Grava a inscrição (ou devolve a que já foi gravada com o mesmo código de envio) e avisa a pessoa por e-mail.</summary>
    public async Task<Inscricao> EnviarAsync(string contaId, Guid temporadaId, Guid codigoDeEnvio, FichaDoSite ficha, bool aceiteDados, bool aceiteImagem,
        string versaoTermos, IReadOnlyCollection<Guid> categoriaIds, DateTime agora, CancellationToken ct)
    {
        if (!aceiteDados || !aceiteImagem)
            throw new DomainException("As duas autorizações (dados pessoais e uso de imagem) são necessárias para se inscrever");
        if (codigoDeEnvio == Guid.Empty) throw new DomainException("Código de envio inválido. Recarregue a página e tente de novo");
        if (categoriaIds.Count == 0) throw new DomainException("Escolha ao menos uma categoria");

        var pessoa = await _pessoas.ObterPorContaAsync(contaId, ct)
            ?? throw new DomainException("Confirme seus dados antes de enviar a inscrição");
        if (Pessoa.EhMenorDeIdade(pessoa.Nascimento, DateOnly.FromDateTime(agora)))
            throw new DomainException("A inscrição de menor de 18 anos é feita pela diretoria, com a autorização do responsável");

        // Reenvio do mesmo envio (duplo clique, recarregar): devolve a inscrição que já foi gravada, sem duplicar.
        var jaGravada = await _inscricoes.ObterPorCodigoDeEnvioAsync(codigoDeEnvio, ct);
        if (jaGravada is not null)
        {
            if (jaGravada.PessoaId != pessoa.Id) throw new DomainException("Código de envio inválido. Recarregue a página e tente de novo");
            return jaGravada;
        }

        var temporada = await _temporadas.ObterPorIdAsync(temporadaId, ct) ?? throw new DomainException("Temporada não encontrada");
        if (!InscricoesAbertas(temporada, agora)) throw new DomainException("As inscrições desta temporada não estão abertas");

        var categorias = new List<TemporadaCategoria>();
        foreach (var id in categoriaIds.Distinct())
        {
            var tc = temporada.Categorias.FirstOrDefault(c => c.Id == id) ?? throw new DomainException("Categoria não pertence a esta temporada");
            if (await _inscricoes.ExisteAtivaAsync(pessoa.Id, tc.Id, ct))
                throw new DomainException($"Você já tem um pedido ativo na categoria {tc.Nome}. Veja em Minhas inscrições");
            categorias.Add(tc);
        }

        var inscricao = new Inscricao(pessoa.Id, temporada.Id, ficha.AlturaCm, ficha.PesoKg, ficha.Posicao, ficha.PossuiPlanoSaude, ficha.NomePlanoSaude, aceiteDados);
        foreach (var tc in categorias) inscricao.AdicionarCategoria(tc.Id);

        var numero = await _inscricoes.ContarProtocolosAsync(temporada.Id, ct) + 1;
        inscricao.RegistrarEnvioPeloSite($"{temporada.Ano}-{numero:D4}", DateTime.UtcNow, versaoTermos, codigoDeEnvio);

        _inscricoes.Adicionar(inscricao);
        try
        {
            await _inscricoes.UnitOfWork.Commit();
        }
        catch (DbUpdateException ex)
        {
            _log.LogWarning(ex, "Conflito ao gravar a inscrição (protocolo ou código de envio repetido)");
            throw new DomainException("Não foi possível concluir o envio agora. Confira em Minhas inscrições e, se não aparecer, tente de novo");
        }

        await AvisarInscricaoRecebidaAsync(pessoa, temporada, inscricao, categorias, ct);
        return inscricao;
    }

    private async Task AvisarInscricaoRecebidaAsync(Pessoa pessoa, Temporada temporada, Inscricao inscricao, IReadOnlyList<TemporadaCategoria> categorias, CancellationToken ct)
    {
        try
        {
            await _email.EnviarAsync(pessoa.Email, $"Inscrição recebida — #{inscricao.Protocolo}",
                $"Olá, {pessoa.Nome.Split(' ')[0]}!\n\nRecebemos o seu pedido de inscrição no {temporada.Campeonato?.Nome} {temporada.Ano} " +
                $"({string.Join(", ", categorias.Select(c => c.Nome))}).\n\nProtocolo: #{inscricao.Protocolo}\n\n" +
                "A diretoria vai analisar o pedido e você será avisado a cada mudança de situação. Nada é cobrado agora: se for aprovado, a associação envia as instruções de pagamento.",
                ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enviar o e-mail de inscrição recebida (protocolo {Protocolo})", inscricao.Protocolo);
        }
    }
}

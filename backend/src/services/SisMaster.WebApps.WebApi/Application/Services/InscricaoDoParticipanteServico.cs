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

    // ------------------------------------------------------------------ minhas inscrições (I6)

    /// <summary>Situação resumida da ficha: Aguardando pagamento (algum pedido aprovado), Em análise, Efetivada ou Recusada (todos recusados).</summary>
    public static string SituacaoDaFicha(IEnumerable<InscricaoCategoria> pedidos)
    {
        var lista = pedidos.ToList();
        if (lista.All(c => c.Status == StatusInscricaoCategoria.Recusada)) return "Recusada";
        var ativas = lista.Where(c => c.Ativa).ToList();
        if (ativas.Any(c => c.Status == StatusInscricaoCategoria.AguardandoPagamento)) return "AguardandoPagamento";
        if (ativas.Any(c => c.Status == StatusInscricaoCategoria.Pendente)) return "EmAnalise";
        return "Efetivada";
    }

    /// <summary>As fichas da conta, da mais recente à mais antiga, com a situação de cada categoria. Conta sem cadastro: lista vazia.</summary>
    public async Task<object> ListarMinhasAsync(string contaId, CancellationToken ct)
    {
        var pessoa = await _pessoas.ObterPorContaAsync(contaId, ct);
        if (pessoa is null) return new { Inscricoes = Array.Empty<object>(), AcaoNecessaria = false };

        var fichas = await _inscricoes.ListarDaPessoaAsync(pessoa.Id, ct);
        var itens = fichas.Select(i =>
        {
            var temporada = i.Categorias.Select(c => c.TemporadaCategoria.Temporada).FirstOrDefault();
            return new
            {
                i.Id,
                i.Protocolo,
                i.Origem,
                EnviadaEm = RelogioLocal.DeUtc(i.DataEnvio),
                TemporadaId = i.TemporadaId,
                Campeonato = temporada?.Campeonato.Nome,
                Ano = temporada?.Ano,
                AssociacaoSigla = temporada?.Campeonato.Associacao.Sigla,
                Situacao = SituacaoDaFicha(i.Categorias),
                Categorias = i.Categorias.OrderBy(c => c.TemporadaCategoria.Nome).Select(c => new { c.Id, Nome = c.TemporadaCategoria.Nome, Status = c.Status.ToString() }).ToList()
            };
        }).ToList();

        return new { Inscricoes = itens, AcaoNecessaria = itens.Any(x => x.Situacao == "AguardandoPagamento") };
    }

    /// <summary>O detalhe de uma ficha da conta: pedidos com motivo de recusa e equipe, dinheiro (só leitura) e dados da ficha. Ficha de outra conta: nulo.</summary>
    public async Task<object?> DetalharMinhaAsync(string contaId, Guid inscricaoId, CancellationToken ct)
    {
        var pessoa = await _pessoas.ObterPorContaAsync(contaId, ct);
        if (pessoa is null) return null;
        var i = await _inscricoes.ObterDaPessoaAsync(inscricaoId, pessoa.Id, ct);
        if (i is null) return null;

        var temporada = i.Categorias.Select(c => c.TemporadaCategoria.Temporada).FirstOrDefault();
        var equipes = await _inscricoes.ListarEquipesDosPedidosAsync(i.Categorias.Select(c => c.Id).ToList(), ct);
        var algumPedidoAprovado = i.Categorias.Any(c => c.Status is StatusInscricaoCategoria.AguardandoPagamento or StatusInscricaoCategoria.Efetivada);
        var todosRecusados = i.Categorias.All(c => !c.Ativa);

        return new
        {
            i.Id,
            i.Protocolo,
            i.Origem,
            EnviadaEm = RelogioLocal.DeUtc(i.DataEnvio),
            Campeonato = temporada?.Campeonato.Nome,
            Ano = temporada?.Ano,
            TemporadaId = i.TemporadaId,
            Situacao = SituacaoDaFicha(i.Categorias),
            Categorias = i.Categorias.OrderBy(c => c.TemporadaCategoria.Nome).Select(c => new
            {
                c.Id,
                Nome = c.TemporadaCategoria.Nome,
                Status = c.Status.ToString(),
                c.MotivoRecusa,
                RecusadaEm = RelogioLocal.DeUtc(c.RecusadaEm),
                Equipe = equipes.TryGetValue(c.Id, out var eq) ? eq : null
            }).ToList(),
            // Dinheiro só para leitura: sem pagamento online. A taxa só importa quando algum pedido foi aprovado.
            Financeiro = todosRecusados ? null : new
            {
                Taxa = temporada?.TaxaInscricao,
                Pago = i.TotalPago,
                TaxaPaga = i.TaxaPaga,
                AlgumPedidoAprovado = algumPedidoAprovado,
                CobrancaGerada = i.CobrancaGerada,
                ValorTotal = i.ValorTotal,
                Desconto = i.ValorDesconto,
                ValorFinal = i.ValorFinal,
                Saldo = i.Saldo
            },
            Ficha = new
            {
                i.AlturaCm, i.PesoKg, i.Posicao, i.PossuiPlanoSaude, i.NomePlanoSaude,
                AutorizouDados = RelogioLocal.DeUtc(i.ConsentimentoLgpdEm),
                AutorizouImagem = RelogioLocal.DeUtc(i.ConsentimentoImagemEm),
                i.VersaoTermos
            }
        };
    }

    // ------------------------------------------------------------------ minha conta (I8)

    /// <summary>Os dados do cadastro ligado à conta e as autorizações dadas em cada inscrição (data e versão do termo). Conta sem cadastro: Pessoa nula.</summary>
    public async Task<object> MinhaContaAsync(string contaId, CancellationToken ct)
    {
        var pessoa = await _pessoas.ObterPorContaAsync(contaId, ct);
        if (pessoa is null) return new { Pessoa = (object?)null, Autorizacoes = Array.Empty<object>() };

        var fichas = await _inscricoes.ListarDaPessoaAsync(pessoa.Id, ct);
        var autorizacoes = fichas.Select(i =>
        {
            var temporada = i.Categorias.Select(c => c.TemporadaCategoria.Temporada).FirstOrDefault();
            return new
            {
                i.Protocolo,
                Campeonato = temporada?.Campeonato.Nome,
                Ano = temporada?.Ano,
                Dados = RelogioLocal.DeUtc(i.ConsentimentoLgpdEm),
                Imagem = RelogioLocal.DeUtc(i.ConsentimentoImagemEm),
                Versao = i.VersaoTermos
            };
        }).ToList();

        return new
        {
            Pessoa = new
            {
                pessoa.Nome,
                CpfMascarado = Mascarar(pessoa.Cpf.Numero),
                Nascimento = pessoa.Nascimento.ToString("yyyy-MM-dd"),
                Sexo = pessoa.Sexo.ToString(),
                pessoa.Telefone,
                pessoa.Email
            },
            Autorizacoes = autorizacoes
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

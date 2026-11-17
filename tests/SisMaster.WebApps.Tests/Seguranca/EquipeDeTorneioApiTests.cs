using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// Fase 2 — a inscrição de equipe em torneio aberto: o responsável envia a equipe e os atletas, cada atleta confirma o convite com a própria conta,
/// a diretoria aprova a equipe e a taxa por atleta o efetiva e o coloca no elenco. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class EquipeDeTorneioApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private static int _semente = 61000;
    private readonly ApiFactory _fabrica;

    public EquipeDeTorneioApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static string CpfValido(int n)
    {
        var b = (100000000 + n * 7919 % 800000000).ToString()[..9].Select(c => c - '0').ToArray();
        int Dv(int[] a) { var s = a.Select((x, i) => x * (a.Length + 1 - i)).Sum(); var r = s * 10 % 11; return r == 10 ? 0 : r; }
        var d1 = Dv(b);
        var d2 = Dv([.. b, d1]);
        return string.Concat(b) + d1 + d2;
    }

    private static JsonElement Dados(JsonElement j) => j.ValueKind == JsonValueKind.Object && j.TryGetProperty("data", out var d) ? d : j;

    private static async Task<(HttpStatusCode Status, JsonElement Json, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        JsonElement json = default;
        if (!string.IsNullOrWhiteSpace(texto)) { try { json = JsonDocument.Parse(texto).RootElement.Clone(); } catch { } }
        return (resp.StatusCode, json, texto);
    }

    private sealed record Cenario(HttpClient Admin, Guid TemporadaId, Guid CategoriaMaster);

    /// <summary>Associação com a categoria Master 40+, temporada de torneio ABERTO com inscrições abertas hoje e taxa de R$ 150.</summary>
    private async Task<Cenario> NovoAsync(string sigla)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var send = (HttpMethod m, string u, object? b) => DadosDeTeste.Enviar(admin, m, u, b);
        await send(HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        var assoc = (await send(HttpMethod.Get, "/api/associacoes", null)).EnumerateArray().First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
            new { nome = "Master 40+", idadeMinima = 40, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var cat = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/categorias", null)).EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Torneio de Verão" });
        var camp = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var hoje = DateTime.Now;
        await send(HttpMethod.Post, $"/api/campeonatos/{camp}/temporadas", new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(-1), dataFimInscricoes = hoje.AddDays(20) });
        var temp = (await send(HttpMethod.Get, $"/api/campeonatos/{camp}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/temporadas/{temp}/categorias", new { categoriaId = cat });
        var tc = (await send(HttpMethod.Get, $"/api/temporadas/{temp}", null)).GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Patch, $"/api/temporadas/{temp}/publico", new { publico = "Aberto" });
        await send(HttpMethod.Patch, $"/api/temporadas/{temp}/taxa-inscricao", new { taxa = 150 });
        return new Cenario(admin, temp, tc);
    }

    private sealed record Atleta(string Nome, string Cpf, string Email, string Nascimento = "1980-03-15");

    private static Atleta NovoAtleta(string nome) => new(nome, CpfValido(_semente++), $"{nome.ToLowerInvariant().Replace(' ', '.')}.{Guid.NewGuid():N}@exemplo.com");

    private static object Corpo(Atleta a) => new { nome = a.Nome, cpf = a.Cpf, nascimento = a.Nascimento, sexo = "Masculino", email = a.Email };

    private HttpClient ContaDe(string email, Guid? id = null) => _fabrica.ClienteCom(ApiFactory.TokenDoParticipante(id ?? Guid.NewGuid(), email));

    private static object Equipe(Cenario c, string nome, IEnumerable<Atleta> atletas, Guid? codigo = null) => new
    {
        temporadaId = c.TemporadaId, temporadaCategoriaId = c.CategoriaMaster, codigoDeEnvio = codigo ?? Guid.NewGuid(), nome,
        responsavelNome = "Maria Responsável", responsavelTelefone = "71999990000", responsavelJoga = false,
        atletas = atletas.Select(Corpo).ToList()
    };

    /// <summary>O código do link que o atleta recebeu por e-mail.</summary>
    private string CodigoDoConvite(string email)
    {
        (string Para, string Assunto, string Corpo) m;
        lock (_fabrica.Emails.Enviados) m = _fabrica.Emails.Enviados.Last(e => e.Para == email && e.Assunto.StartsWith("Convite para a equipe"));
        return Regex.Match(m.Corpo, @"/convites/(\S+)").Groups[1].Value;
    }

    private static object Confirmacao() => new
    {
        telefone = "71988887777", alturaCm = 180, pesoKg = 80, posicao = "Ala", possuiPlanoSaude = false, aceiteDados = true, aceiteImagem = true, versaoTermos = "1.0"
    };

    private async Task<(Guid EquipeId, List<(Atleta Atleta, HttpClient Conta)> Atletas)> EquipeConfirmadaAsync(Cenario c, string nome, int quantos, HttpClient responsavel)
    {
        var atletas = Enumerable.Range(1, quantos).Select(i => NovoAtleta($"Atleta{i} {nome}")).ToList();
        var criada = await Chamar(responsavel, HttpMethod.Post, "/api/participante/equipes", Equipe(c, nome, atletas));
        criada.Status.Should().Be(HttpStatusCode.OK, criada.Corpo);
        var id = Dados(criada.Json).GetProperty("id").GetGuid();
        var contas = new List<(Atleta, HttpClient)>();
        foreach (var a in atletas)
        {
            var conta = ContaDe(a.Email);
            var r = await Chamar(conta, HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(a.Email)}/confirmar", Confirmacao());
            r.Status.Should().Be(HttpStatusCode.OK, r.Corpo);
            contas.Add((a, conta));
        }
        return (id, contas);
    }

    private async Task<JsonElement> DetalheDiretoriaAsync(Cenario c, Guid id) =>
        Dados((await Chamar(c.Admin, HttpMethod.Get, $"/api/equipes-inscritas/{id}")).Json);

    // ------------------------------------------------------------------ fluxo completo

    [Fact]
    public async Task Equipe_enviada_confirmada_e_aprovada_entra_no_elenco_atleta_a_atleta_conforme_a_taxa()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqFluxo");
        var responsavel = ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com");

        var (id, atletas) = await EquipeConfirmadaAsync(c, "Veteranos do Sul", 5, responsavel);

        // protocolo próprio da equipe e um e-mail de convite para cada atleta
        var det = await DetalheDiretoriaAsync(c, id);
        det.GetProperty("protocolo").GetString().Should().Be($"E{DateTime.Now.Year}-0001");
        det.GetProperty("situacao").GetString().Should().Be("EmAnalise", "5 confirmados e nenhum convite em aberto: só falta a diretoria");
        foreach (var (a, _) in atletas) _fabrica.Emails.Quantos(a.Email, "Convite para a equipe").Should().Be(1);

        // aprovar: cria a equipe (ainda vazia) e aprova os pedidos; o atleta só entra no elenco quando a taxa dele é registrada
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/aprovar", new { })).Status.Should().Be(HttpStatusCode.OK);
        var elenco = async () => Dados((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/equipes")).Json)
            .GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray().Single(e => e.GetProperty("nome").GetString() == "Veteranos do Sul")
            .GetProperty("atletas").GetArrayLength();
        (await elenco()).Should().Be(0);

        det = await DetalheDiretoriaAsync(c, id);
        det.GetProperty("situacao").GetString().Should().Be("Aprovada");
        var fichas = det.GetProperty("atletas").EnumerateArray().Select(a => a.GetProperty("fichaId").GetGuid()).ToList();
        var lote = await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/taxas", new { dataPagamento = "2027-01-05", inscricaoIds = fichas.Take(3) });
        Dados(lote.Json).GetProperty("resultados").EnumerateArray().Should().OnlyContain(r => r.GetProperty("ok").GetBoolean());
        (await elenco()).Should().Be(3, "só quem pagou a taxa entra: a equipe não espera os outros");

        // o quarto paga depois, pelo caminho individual de sempre: também entra
        await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{fichas[3]}/pagamentos/taxa", new { dataPagamento = "2027-01-06" });
        (await elenco()).Should().Be(4);

        // torneio aberto: ninguém virou associado
        Dados((await Chamar(atletas[0].Conta, HttpMethod.Get, "/api/participante/conta")).Json).GetProperty("temporadas").EnumerateArray()
            .Single().GetProperty("situacao").GetString().Should().Be("Participante");

        // o responsável acompanha a taxa de cada atleta, sem ver CPF
        var minha = Dados((await Chamar(responsavel, HttpMethod.Get, $"/api/participante/equipes/{id}")).Json);
        minha.GetProperty("atletas").EnumerateArray().Count(a => a.GetProperty("taxaPaga").GetBoolean()).Should().Be(4);
        minha.GetRawText().Should().NotContain(atletas[0].Atleta.Cpf);
    }

    [Fact]
    public async Task Atleta_que_confirma_depois_da_aprovacao_tem_o_pedido_aprovado_na_hora_e_entra_ao_pagar()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqTardio");
        var responsavel = ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com");
        var atletas = Enumerable.Range(1, 6).Select(i => NovoAtleta($"Jogador{i} Tardio")).ToList();
        var criada = await Chamar(responsavel, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Tardios", atletas));
        var id = Dados(criada.Json).GetProperty("id").GetGuid();
        foreach (var a in atletas.Take(5))
            (await Chamar(ContaDe(a.Email), HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(a.Email)}/confirmar", Confirmacao())).Status.Should().Be(HttpStatusCode.OK);
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/aprovar", new { })).Status.Should().Be(HttpStatusCode.OK);

        var sexto = atletas[5];
        var conta = ContaDe(sexto.Email);
        (await Chamar(conta, HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(sexto.Email)}/confirmar", Confirmacao())).Status.Should().Be(HttpStatusCode.OK);
        var det = await DetalheDiretoriaAsync(c, id);
        var dele = det.GetProperty("atletas").EnumerateArray().Single(a => a.GetProperty("nome").GetString() == sexto.Nome);
        dele.GetProperty("pedido").GetString().Should().Be("AguardandoPagamento", "a equipe já estava aprovada");

        await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{dele.GetProperty("fichaId").GetGuid()}/pagamentos/taxa", new { dataPagamento = "2027-01-06" });
        Dados((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/equipes")).Json).GetProperty("categorias")[0].GetProperty("equipes")[0]
            .GetProperty("atletas").GetArrayLength().Should().Be(1);
    }

    // ------------------------------------------------------------------ regras do envio

    [Fact]
    public async Task Envio_exige_elenco_de_5_a_15_cpf_real_maior_de_idade_e_sem_repeticao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqRegras");
        var resp = ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com");
        var cinco = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Regra{i} Elenco")).ToList();

        var poucos = await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Poucos", cinco.Take(4)));
        poucos.Status.Should().Be(HttpStatusCode.BadRequest);
        poucos.Corpo.Should().Contain("pelo menos 5");

        var muitos = Enumerable.Range(1, 16).Select(i => NovoAtleta($"Muito{i} Elenco")).ToList();
        (await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Muitos", muitos))).Corpo.Should().Contain("no máximo 15");

        var cpfRuim = cinco.Take(4).Append(cinco[4] with { Cpf = "12345678900" });
        (await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "CpfRuim", cpfRuim))).Corpo.Should().Contain("CPF inválido");

        var menor = cinco.Take(4).Append(cinco[4] with { Nascimento = DateTime.Now.AddYears(-15).ToString("yyyy-MM-dd") });
        (await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Menor", menor))).Corpo.Should().Contain("menor de 18");

        var repetido = cinco.Take(4).Append(cinco[0] with { Email = "outro@exemplo.com" });
        (await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Repetido", repetido))).Corpo.Should().Contain("CPF repetido");

        // nome repetido na temporada e reenvio com o mesmo código de envio
        var codigo = Guid.NewGuid();
        var ok = await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Única", cinco, codigo));
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Corpo);
        var igual = await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Única", cinco, codigo));
        Dados(igual.Json).GetProperty("id").GetGuid().Should().Be(Dados(ok.Json).GetProperty("id").GetGuid(), "o mesmo envio não duplica");
        var outra = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Outra{i} Elenco")).ToList();
        (await Chamar(ContaDe($"r2.{Guid.NewGuid():N}@exemplo.com"), HttpMethod.Post, "/api/participante/equipes", Equipe(c, "única", outra))).Corpo.Should().Contain("Já existe uma equipe");

        // atleta já convidado em outra equipe da categoria
        var comConflito = outra.Take(4).Append(cinco[1] with { Email = "x@exemplo.com" });
        (await Chamar(ContaDe($"r3.{Guid.NewGuid():N}@exemplo.com"), HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Conflito", comConflito))).Corpo.Should().Contain("já está em outra equipe");
    }

    [Fact]
    public async Task So_funciona_em_torneio_aberto_com_inscricoes_abertas()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqFechado");
        await Chamar(c.Admin, HttpMethod.Patch, $"/api/temporadas/{c.TemporadaId}/publico", new { publico = "Associados" });
        var atletas = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Fechado{i} Elenco")).ToList();
        var r = await Chamar(ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com"), HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Sem Torneio", atletas));
        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("não aceita inscrição de equipe");
    }

    // ------------------------------------------------------------------ convite

    [Fact]
    public async Task Link_do_convite_mostra_o_minimo_sem_login_e_so_confirma_a_conta_do_email_convidado()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqConvite");
        var atletas = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Convite{i} Elenco")).ToList();
        await Chamar(ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com"), HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Convidados", atletas));
        var codigo = CodigoDoConvite(atletas[0].Email);

        var anonimo = _fabrica.CreateClient();
        var vista = await Chamar(anonimo, HttpMethod.Get, $"/api/convites/{codigo}");
        vista.Status.Should().Be(HttpStatusCode.OK, vista.Corpo);
        vista.Json.GetProperty("equipe").GetString().Should().Be("Convidados");
        vista.Json.GetProperty("situacao").GetString().Should().Be("Enviado");
        vista.Corpo.Should().NotContain(atletas[0].Cpf).And.NotContain(atletas[0].Email, "o e-mail aparece só mascarado");
        (await Chamar(anonimo, HttpMethod.Get, "/api/convites/codigo-que-nao-existe")).Status.Should().Be(HttpStatusCode.NotFound);

        // conta de outro e-mail não confirma o convite alheio
        var intrusa = await Chamar(ContaDe("intrusa@exemplo.com"), HttpMethod.Post, $"/api/participante/convites/{codigo}/confirmar", Confirmacao());
        intrusa.Status.Should().Be(HttpStatusCode.BadRequest);
        intrusa.Corpo.Should().Contain("Entre no site com a conta desse e-mail");

        // sem as duas autorizações não confirma
        var semAceite = await Chamar(ContaDe(atletas[0].Email), HttpMethod.Post, $"/api/participante/convites/{codigo}/confirmar",
            new { aceiteDados = true, aceiteImagem = false, versaoTermos = "1.0" });
        semAceite.Corpo.Should().Contain("duas autorizações");
    }

    [Fact]
    public async Task Recusar_o_convite_apaga_os_dados_do_atleta_e_avisa_o_responsavel()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqRecusa");
        var emailResp = $"resp.{Guid.NewGuid():N}@exemplo.com";
        var resp = ContaDe(emailResp);
        var atletas = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Recusa{i} Elenco")).ToList();
        var id = Dados((await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Recusas", atletas))).Json).GetProperty("id").GetGuid();
        var codigo = CodigoDoConvite(atletas[0].Email);

        (await Chamar(_fabrica.CreateClient(), HttpMethod.Post, $"/api/convites/{codigo}/recusar")).Status.Should().Be(HttpStatusCode.OK);
        _fabrica.Emails.Quantos(emailResp, "Recusa1 recusou o convite").Should().Be(1);

        var det = await DetalheDiretoriaAsync(c, id);
        var dele = det.GetProperty("atletas").EnumerateArray().Single(a => a.GetProperty("nome").GetString() == "Recusa1");
        dele.GetProperty("status").GetString().Should().Be("Recusou");
        det.GetRawText().Should().NotContain(atletas[0].Cpf, "quem recusa não deixa CPF guardado");
        (await Chamar(_fabrica.CreateClient(), HttpMethod.Post, $"/api/convites/{codigo}/recusar")).Status.Should().Be(HttpStatusCode.NotFound, "o link de quem recusou deixa de valer");
    }

    // ------------------------------------------------------------------ diretoria

    [Fact]
    public async Task Aprovar_exige_5_confirmados_e_recusar_cancela_os_pedidos_dos_confirmados()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqDirec");
        var resp = ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com");
        var atletas = Enumerable.Range(1, 6).Select(i => NovoAtleta($"Diretoria{i} Elenco")).ToList();
        var id = Dados((await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Analisadas", atletas))).Json).GetProperty("id").GetGuid();
        foreach (var a in atletas.Take(4))
            await Chamar(ContaDe(a.Email), HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(a.Email)}/confirmar", Confirmacao());

        var cedo = await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/aprovar", new { });
        cedo.Status.Should().Be(HttpStatusCode.BadRequest);
        cedo.Corpo.Should().Contain("pelo menos 5 atletas confirmados");
        (await DetalheDiretoriaAsync(c, id)).GetProperty("situacao").GetString().Should().Be("AguardandoConfirmacoes");

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/recusar", new { motivo = "" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/recusar", new { motivo = "Faltam atletas" })).Status.Should().Be(HttpStatusCode.OK);
        (await DetalheDiretoriaAsync(c, id)).GetProperty("situacao").GetString().Should().Be("Recusada");

        // os pedidos dos que tinham confirmado foram recusados e quem ainda tinha convite aberto não confirma mais
        var fichas = Dados((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/inscricoes")).Json).EnumerateArray().ToList();
        fichas.Should().HaveCount(4);
        fichas.SelectMany(f => f.GetProperty("categorias").EnumerateArray()).Should().OnlyContain(x => x.GetProperty("status").GetString() == "Recusada");
        var tarde = await Chamar(ContaDe(atletas[5].Email), HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(atletas[5].Email)}/confirmar", Confirmacao());
        tarde.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Taxa_em_lote_registra_so_dos_atletas_da_equipe_e_informa_o_que_falhou()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqLote");
        var (id, atletas) = await EquipeConfirmadaAsync(c, "Em Lote", 5, ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com"));
        await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/aprovar", new { });
        var fichas = (await DetalheDiretoriaAsync(c, id)).GetProperty("atletas").EnumerateArray().Select(a => a.GetProperty("fichaId").GetGuid()).ToList();

        await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{fichas[0]}/pagamentos/taxa", new { dataPagamento = "2027-01-05" });
        var lote = Dados((await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes-inscritas/{id}/taxas",
            new { dataPagamento = "2027-01-05", inscricaoIds = new[] { fichas[0], fichas[1], Guid.NewGuid() } })).Json).GetProperty("resultados").EnumerateArray().ToList();
        lote[0].GetProperty("ok").GetBoolean().Should().BeFalse("a taxa dele já estava paga");
        lote[0].GetProperty("erro").GetString().Should().Contain("já foi paga");
        lote[1].GetProperty("ok").GetBoolean().Should().BeTrue();
        lote[2].GetProperty("erro").GetString().Should().Contain("não pertence a esta equipe");
    }

    // ------------------------------------------------------------------ isolamento

    [Fact]
    public async Task Cada_responsavel_ve_so_a_propria_equipe_e_a_gestao_nao_e_do_participante()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqIsola");
        var dono = ContaDe($"dono.{Guid.NewGuid():N}@exemplo.com");
        var atletas = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Isola{i} Elenco")).ToList();
        var id = Dados((await Chamar(dono, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Isolada", atletas))).Json).GetProperty("id").GetGuid();

        var outro = ContaDe($"outro.{Guid.NewGuid():N}@exemplo.com");
        (await Chamar(outro, HttpMethod.Get, $"/api/participante/equipes/{id}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Chamar(outro, HttpMethod.Post, $"/api/participante/equipes/{id}/atletas", Corpo(NovoAtleta("Invasor Elenco")))).Status.Should().Be(HttpStatusCode.NotFound);
        Dados((await Chamar(outro, HttpMethod.Get, "/api/participante/equipes")).Json).GetProperty("equipes").GetArrayLength().Should().Be(0);

        (await Chamar(dono, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/equipes-inscritas")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(dono, HttpMethod.Post, $"/api/equipes-inscritas/{id}/aprovar", new { })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ gestão do elenco pelo responsável

    [Fact]
    public async Task Responsavel_adiciona_reenvia_e_remove_atleta_ate_a_confirmacao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EqGestao");
        var resp = ContaDe($"resp.{Guid.NewGuid():N}@exemplo.com");
        var atletas = Enumerable.Range(1, 5).Select(i => NovoAtleta($"Gestao{i} Elenco")).ToList();
        var id = Dados((await Chamar(resp, HttpMethod.Post, "/api/participante/equipes", Equipe(c, "Gerida", atletas))).Json).GetProperty("id").GetGuid();

        var novo = NovoAtleta("Gestao6 Elenco");
        (await Chamar(resp, HttpMethod.Post, $"/api/participante/equipes/{id}/atletas", Corpo(novo))).Status.Should().Be(HttpStatusCode.OK);
        _fabrica.Emails.Quantos(novo.Email, "Convite para a equipe").Should().Be(1);

        var minha = Dados((await Chamar(resp, HttpMethod.Get, $"/api/participante/equipes/{id}")).Json);
        var conviteId = minha.GetProperty("atletas").EnumerateArray().Single(a => a.GetProperty("nome").GetString() == "Gestao6 Elenco").GetProperty("id").GetGuid();

        // novo link invalida o anterior
        var antigo = CodigoDoConvite(novo.Email);
        (await Chamar(resp, HttpMethod.Post, $"/api/participante/equipes/{id}/atletas/{conviteId}/reenviar")).Status.Should().Be(HttpStatusCode.OK);
        var atual = CodigoDoConvite(novo.Email);
        atual.Should().NotBe(antigo);
        (await Chamar(_fabrica.CreateClient(), HttpMethod.Get, $"/api/convites/{antigo}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Chamar(_fabrica.CreateClient(), HttpMethod.Get, $"/api/convites/{atual}")).Status.Should().Be(HttpStatusCode.OK);

        // quem ainda não confirmou pode ser removido; quem confirmou, não
        (await Chamar(resp, HttpMethod.Delete, $"/api/participante/equipes/{id}/atletas/{conviteId}")).Status.Should().Be(HttpStatusCode.OK);
        await Chamar(ContaDe(atletas[0].Email), HttpMethod.Post, $"/api/participante/convites/{CodigoDoConvite(atletas[0].Email)}/confirmar", Confirmacao());
        var confirmado = Dados((await Chamar(resp, HttpMethod.Get, $"/api/participante/equipes/{id}")).Json).GetProperty("atletas").EnumerateArray()
            .Single(a => a.GetProperty("nome").GetString() == "Gestao1 Elenco").GetProperty("id").GetGuid();
        var r = await Chamar(resp, HttpMethod.Delete, $"/api/participante/equipes/{id}/atletas/{confirmado}");
        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("já confirmou");
    }
}

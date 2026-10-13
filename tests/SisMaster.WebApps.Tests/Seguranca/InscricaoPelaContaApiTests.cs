using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// I2/I3/I5 — a inscrição pela conta do site: ligar a conta ao cadastro pelo CPF sem revelar nem sobrescrever dados, menor de 18, preparo
/// do formulário (ficha anterior, pedidos ativos), envio com protocolo, duas autorizações, idempotência, janela de inscrição, e-mail de
/// "inscrição recebida" e isolamento do Participante. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class InscricaoPelaContaApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private static int _semente = 41000;
    private readonly ApiFactory _fabrica;

    public InscricaoPelaContaApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static string CpfValido(int n)
    {
        var b = (100000000 + n * 7919 % 800000000).ToString()[..9].Select(c => c - '0').ToArray();
        int Dv(int[] a) { var s = a.Select((x, i) => x * (a.Length + 1 - i)).Sum(); var r = s * 10 % 11; return r == 10 ? 0 : r; }
        var d1 = Dv(b);
        var d2 = Dv([.. b, d1]);
        return string.Concat(b) + d1 + d2;
    }

    private sealed record Cenario(HttpClient Admin, Guid TemporadaId, Guid CampeonatoId, Guid CategoriaMaster, Guid CategoriaHiper, string Ano);

    /// <summary>Associação com duas categorias (Master 40+ e Hipermaster 60+, masculinas) e uma temporada com inscrições abertas hoje.</summary>
    private async Task<Cenario> NovoAsync(string sigla, int inicioEmDias = -1, int fimEmDias = 20)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var send = (HttpMethod m, string u, object? b) => DadosDeTeste.Enviar(admin, m, u, b);
        await send(HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        var assoc = (await send(HttpMethod.Get, "/api/associacoes", null)).EnumerateArray().First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
        foreach (var (nome, idade) in new[] { ("Master 40+", 40), ("Hipermaster 60+", 60) })
            await send(HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
                new { nome, idadeMinima = idade, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var cats = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/categorias", null)).EnumerateArray().ToList();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato do Site" });
        var camp = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var hoje = DateTime.Now;
        await send(HttpMethod.Post, $"/api/campeonatos/{camp}/temporadas", new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(inicioEmDias), dataFimInscricoes = hoje.AddDays(fimEmDias) });
        var temp = (await send(HttpMethod.Get, $"/api/campeonatos/{camp}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        foreach (var c in cats) await send(HttpMethod.Post, $"/api/temporadas/{temp}/categorias", new { categoriaId = c.GetProperty("id").GetGuid() });
        var tcs = (await send(HttpMethod.Get, $"/api/temporadas/{temp}", null)).GetProperty("categorias").EnumerateArray().ToList();
        Guid Tc(string nome) => tcs.First(t => t.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();
        return new Cenario(admin, temp, camp, Tc("Master 40+"), Tc("Hipermaster 60+"), hoje.Year.ToString());
    }

    private HttpClient Conta(out Guid id, out string email)
    {
        id = Guid.NewGuid();
        email = $"conta.{id:N}@exemplo.com";
        return _fabrica.ClienteCom(ApiFactory.TokenDoParticipante(id, email));
    }

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

    private static object Vinculo(string cpf, string nascimento = "1975-05-10", string nome = "José Santana", string? telefone = "71999990000", string sexo = "Masculino") =>
        new { nome, cpf, nascimento, sexo, telefone };

    private static object Envio(Cenario c, Guid codigo, IEnumerable<Guid> categorias, bool dados = true, bool imagem = true) => new
    {
        temporadaId = c.TemporadaId, codigoDeEnvio = codigo, alturaCm = 182, pesoKg = 84, posicao = "Ala", possuiPlanoSaude = false,
        aceiteDados = dados, aceiteImagem = imagem, versaoTermos = "1.0", categoriaIds = categorias,
    };

    // ------------------------------------------------------------------ vínculo (I2)

    [Fact]
    public async Task Cpf_novo_cria_o_cadastro_ligado_a_conta_e_a_segunda_chamada_devolve_o_mesmo()
    {
        if (SemBanco) return;
        var conta = Conta(out _, out _);
        var cpf = CpfValido(_semente++);

        var r = await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf));

        r.Status.Should().Be(HttpStatusCode.OK, r.Corpo);
        r.Json.GetProperty("situacao").GetString().Should().Be("Vinculada");
        r.Json.GetProperty("pessoa").GetProperty("cpfMascarado").GetString().Should().Be($"***.{cpf[3..6]}.{cpf[6..9]}-**");
        r.Json.GetRawText().Should().NotContain(cpf, "o CPF inteiro nunca volta");

        // A conta já tem cadastro: outro CPF digitado depois é ignorado (só o telefone atualiza).
        var de_novo = await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), telefone: "71888880000"));
        de_novo.Json.GetProperty("pessoa").GetProperty("cpfMascarado").GetString().Should().Be($"***.{cpf[3..6]}.{cpf[6..9]}-**");
        de_novo.Json.GetProperty("pessoa").GetProperty("telefone").GetString().Should().Be("71888880000");
    }

    [Fact]
    public async Task Cpf_existente_com_o_mesmo_email_da_conta_liga_sem_sobrescrever_os_dados_do_cadastro()
    {
        if (SemBanco) return;
        var cenario = await NovoAsync("VinMesmo");
        var conta = Conta(out _, out var email);
        var cpf = CpfValido(_semente++);
        // cadastro criado pela diretoria, com o mesmo e-mail da conta
        await DadosDeTeste.Enviar(cenario.Admin, HttpMethod.Post, "/api/inscricoes", new
        {
            temporadaId = cenario.TemporadaId, nome = "Nome da Diretoria", cpf, nascimento = "1970-01-02", sexo = "Masculino", email,
            telefone = "71911110000", consentimentoLgpd = true, temporadaCategoriaIds = new[] { cenario.CategoriaMaster },
        });

        var r = await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf, nascimento: "1990-09-09", nome: "Outro Nome Digitado"));

        r.Json.GetProperty("situacao").GetString().Should().Be("Vinculada");
        var pessoa = r.Json.GetProperty("pessoa");
        pessoa.GetProperty("nome").GetString().Should().Be("Nome da Diretoria", "o site nunca sobrescreve o cadastro");
        pessoa.GetProperty("nascimento").GetString().Should().Be("1970-01-02");
    }

    [Fact]
    public async Task Cpf_de_outro_cadastro_ou_de_outra_conta_e_bloqueado_com_a_mesma_resposta_e_sem_dado_nenhum()
    {
        if (SemBanco) return;
        var cenario = await NovoAsync("VinBloq");
        var cpf = CpfValido(_semente++);
        await DadosDeTeste.Enviar(cenario.Admin, HttpMethod.Post, "/api/inscricoes", new
        {
            temporadaId = cenario.TemporadaId, nome = "Dona do Cadastro", cpf, nascimento = "1970-01-02", sexo = "Masculino", email = "dona@exemplo.com",
            telefone = "71911110000", consentimentoLgpd = true, temporadaCategoriaIds = new[] { cenario.CategoriaMaster },
        });

        // 1) cadastro existe com OUTRO e-mail
        var intrusa = Conta(out _, out _);
        var a = await Chamar(intrusa, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf));
        a.Json.GetProperty("situacao").GetString().Should().Be("Bloqueada");
        a.Json.GetProperty("pessoa").ValueKind.Should().Be(JsonValueKind.Null);
        a.Corpo.Should().NotContain("Dona do Cadastro").And.NotContain("dona@exemplo.com");

        // 2) o dono liga a conta dele; outra conta com o MESMO e-mail do cadastro tenta o mesmo CPF: bloqueada igual
        var dono = _fabrica.ClienteCom(ApiFactory.TokenDoParticipante(Guid.NewGuid(), "dona@exemplo.com"));
        (await Chamar(dono, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf))).Json.GetProperty("situacao").GetString().Should().Be("Vinculada");
        var outraConta = _fabrica.ClienteCom(ApiFactory.TokenDoParticipante(Guid.NewGuid(), "dona@exemplo.com"));
        var b = await Chamar(outraConta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf));
        b.Json.GetProperty("situacao").GetString().Should().Be("Bloqueada");
        b.Corpo.Should().Be(a.Corpo, "as duas recusas são idênticas: não revelam o motivo");
    }

    [Fact]
    public async Task Menor_de_18_anos_e_cpf_invalido_sao_recusados()
    {
        if (SemBanco) return;
        var conta = Conta(out _, out _);
        var menor = await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nascimento: DateTime.Now.AddYears(-15).ToString("yyyy-MM-dd")));
        menor.Json.GetProperty("situacao").GetString().Should().Be("Menor");

        var ruim = await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo("12345678900"));
        ruim.Status.Should().Be(HttpStatusCode.BadRequest);
        ruim.Corpo.Should().Contain("CPF inválido");
        (await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo("11111111111"))).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tentativas_de_vinculo_demais_por_conta_sao_limitadas()
    {
        if (SemBanco) return;
        await using var limitada = _fabrica.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Seguranca:LimiteDeVinculosPorHora"] = "3" })));
        var cliente = limitada.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", ApiFactory.TokenDoParticipante(Guid.NewGuid(), "limite@exemplo.com"));

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) codigos.Add((await Chamar(cliente, HttpMethod.Post, "/api/participante/vinculo", Vinculo("00000000000"))).Status);

        codigos.Count(s => s == HttpStatusCode.TooManyRequests).Should().Be(2, "só as 3 primeiras tentativas passam");
    }

    // ------------------------------------------------------------------ preparo e envio (I3/I5)

    [Fact]
    public async Task Preparo_mostra_a_temporada_as_categorias_e_depois_de_inscrito_o_pedido_ativo_e_a_ficha_anterior()
    {
        if (SemBanco) return;
        var c = await NovoAsync("PrepSite");
        var conta = Conta(out _, out _);

        var antes = await Chamar(conta, HttpMethod.Get, $"/api/participante/temporadas/{c.TemporadaId}/inscricao/preparo");
        var dados = antes.Json.TryGetProperty("data", out var d) ? d : antes.Json;
        dados.GetProperty("temporada").GetProperty("abertas").GetBoolean().Should().BeTrue();
        dados.GetProperty("pessoa").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("ultimaFicha").ValueKind.Should().Be(JsonValueKind.Null);
        dados.GetProperty("categorias").EnumerateArray().Select(x => x.GetProperty("nome").GetString()).Should().Equal("Hipermaster 60+", "Master 40+");

        await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++)));
        var envio = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]));
        envio.Status.Should().Be(HttpStatusCode.OK, envio.Corpo);

        var depois = await Chamar(conta, HttpMethod.Get, $"/api/participante/temporadas/{c.TemporadaId}/inscricao/preparo");
        var d2 = depois.Json.TryGetProperty("data", out var dd) ? dd : depois.Json;
        d2.GetProperty("ultimaFicha").GetProperty("alturaCm").GetInt32().Should().Be(182);
        d2.GetProperty("ultimaFicha").GetProperty("posicao").GetString().Should().Be("Ala");
        var master = d2.GetProperty("categorias").EnumerateArray().First(x => x.GetProperty("nome").GetString() == "Master 40+");
        master.GetProperty("jaPedida").GetString().Should().StartWith(c.Ano + "-");
        d2.GetProperty("categorias").EnumerateArray().First(x => x.GetProperty("nome").GetString() == "Hipermaster 60+").GetProperty("jaPedida").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Envio_gera_protocolo_sequencial_grava_as_autorizacoes_e_avisa_por_email()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EnvioSite");
        var conta1 = Conta(out _, out var email1);
        var conta2 = Conta(out _, out _);
        await Chamar(conta1, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++)));
        await Chamar(conta2, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nome: "Maria Souza"));

        var a = await Chamar(conta1, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster, c.CategoriaHiper]));
        var b = await Chamar(conta2, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]));

        a.Status.Should().Be(HttpStatusCode.OK, a.Corpo);
        var ja = a.Json.TryGetProperty("data", out var da) ? da : a.Json;
        var jb = b.Json.TryGetProperty("data", out var db) ? db : b.Json;
        ja.GetProperty("protocolo").GetString().Should().Be($"{c.Ano}-0001");
        jb.GetProperty("protocolo").GetString().Should().Be($"{c.Ano}-0002");
        ja.GetProperty("categorias").GetInt32().Should().Be(2);

        _fabrica.Emails.Quantos(email1, "Inscrição recebida").Should().Be(1);
        string corpo;
        lock (_fabrica.Emails.Enviados) corpo = _fabrica.Emails.Enviados.Last(e => e.Para == email1).Corpo;
        corpo.Should().Contain($"#{c.Ano}-0001").And.Contain("Master 40+").And.Contain("Hipermaster 60+");

        // a diretoria enxerga a ficha na gestão, com a origem do site
        var fichas = await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/inscricoes");
        fichas.EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_envio_nao_duplica_e_devolve_o_mesmo_protocolo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("IdemSite");
        var conta = Conta(out _, out var email);
        await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++)));
        var codigo = Guid.NewGuid();

        var um = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, codigo, [c.CategoriaMaster]));
        var dois = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, codigo, [c.CategoriaMaster]));

        dois.Status.Should().Be(HttpStatusCode.OK);
        dois.Corpo.Should().Contain($"{c.Ano}-0001");
        um.Corpo.Should().Contain($"{c.Ano}-0001");
        _fabrica.Emails.Quantos(email, "Inscrição recebida").Should().Be(1, "o reenvio não manda outro e-mail");
        (await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/inscricoes")).EnumerateArray().Should().HaveCount(1);

        // o mesmo código usado por OUTRA conta não devolve a inscrição alheia
        var outra = Conta(out _, out _);
        await Chamar(outra, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nome: "Outra Pessoa"));
        var roubo = await Chamar(outra, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, codigo, [c.CategoriaMaster]));
        roubo.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recusas_do_servidor_autorizacao_categoria_janela_pedido_ativo_cadastro_e_menor()
    {
        if (SemBanco) return;
        var c = await NovoAsync("RecSite");
        var conta = Conta(out _, out _);

        // sem cadastro ligado ainda
        var semCadastro = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]));
        semCadastro.Status.Should().Be(HttpStatusCode.BadRequest);
        semCadastro.Corpo.Should().Contain("Confirme seus dados");

        await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++)));

        var semImagem = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster], imagem: false));
        semImagem.Status.Should().Be(HttpStatusCode.BadRequest);
        semImagem.Corpo.Should().Contain("As duas autorizações");
        (await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster], dados: false))).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), []))).Status.Should().Be(HttpStatusCode.BadRequest);
        var deOutra = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [Guid.NewGuid()]));
        deOutra.Corpo.Should().Contain("Categoria não pertence a esta temporada");

        (await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]))).Status.Should().Be(HttpStatusCode.OK);
        var repetida = await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]));
        repetida.Status.Should().Be(HttpStatusCode.BadRequest);
        repetida.Corpo.Should().Contain("pedido ativo na categoria Master 40+");

        // fora da janela de datas
        var fechada = await NovoAsync("JanSite", inicioEmDias: 5, fimEmDias: 30);
        var conta2 = Conta(out _, out _);
        await Chamar(conta2, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nome: "Quem Chegou Cedo"));
        var cedo = await Chamar(conta2, HttpMethod.Post, "/api/participante/inscricoes", Envio(fechada, Guid.NewGuid(), [fechada.CategoriaMaster]));
        cedo.Status.Should().Be(HttpStatusCode.BadRequest);
        cedo.Corpo.Should().Contain("não estão abertas");

        // inscrições encerradas pela diretoria
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Patch, $"/api/campeonatos/{c.CampeonatoId}/temporadas/{c.TemporadaId}/encerrar-inscricoes", new { });
        var conta3 = Conta(out _, out _);
        await Chamar(conta3, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nome: "Quem Chegou Tarde"));
        (await Chamar(conta3, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]))).Corpo.Should().Contain("não estão abertas");
    }

    [Fact]
    public async Task Menor_de_18_nao_envia_inscricao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("MenorSite");
        var cenarioConta = Conta(out _, out var email);
        var cpf = CpfValido(_semente++);
        // cadastro de um menor feito pela diretoria com o e-mail da conta: liga, mas o envio é recusado
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, "/api/inscricoes", new
        {
            temporadaId = c.TemporadaId, nome = "Jovem Atleta", cpf, nascimento = DateTime.Now.AddYears(-16).ToString("yyyy-MM-dd"), sexo = "Masculino", email,
            consentimentoLgpd = true, temporadaCategoriaIds = new[] { c.CategoriaMaster },
        });
        var v = await Chamar(cenarioConta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(cpf));
        v.Json.GetProperty("situacao").GetString().Should().Be("Menor");

        var e = await Chamar(cenarioConta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaHiper]));
        e.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ minhas inscrições (I6)

    private static JsonElement Dados(JsonElement j) => j.TryGetProperty("data", out var d) ? d : j;

    [Fact]
    public async Task Minhas_inscricoes_mostra_so_as_da_propria_conta_com_a_situacao_de_cada_categoria()
    {
        if (SemBanco) return;
        var c = await NovoAsync("MinhasIns");
        var conta = Conta(out _, out _);
        var outra = Conta(out _, out _);

        // sem cadastro: lista vazia
        var vazia = Dados((await Chamar(conta, HttpMethod.Get, "/api/participante/inscricoes")).Json);
        vazia.GetProperty("inscricoes").GetArrayLength().Should().Be(0);
        vazia.GetProperty("acaoNecessaria").GetBoolean().Should().BeFalse();

        await Chamar(conta, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++)));
        await Chamar(outra, HttpMethod.Post, "/api/participante/vinculo", Vinculo(CpfValido(_semente++), nome: "Maria Souza"));
        await Chamar(conta, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster, c.CategoriaHiper]));
        await Chamar(outra, HttpMethod.Post, "/api/participante/inscricoes", Envio(c, Guid.NewGuid(), [c.CategoriaMaster]));

        var lista = Dados((await Chamar(conta, HttpMethod.Get, "/api/participante/inscricoes")).Json);
        var ficha = lista.GetProperty("inscricoes").EnumerateArray().Single();
        ficha.GetProperty("protocolo").GetString().Should().Be($"{c.Ano}-0001");
        ficha.GetProperty("campeonato").GetString().Should().Be("Campeonato do Site");
        ficha.GetProperty("situacao").GetString().Should().Be("EmAnalise");
        ficha.GetProperty("categorias").EnumerateArray().Select(x => x.GetProperty("nome").GetString()).Should().Equal("Hipermaster 60+", "Master 40+");
        lista.GetProperty("acaoNecessaria").GetBoolean().Should().BeFalse();

        // a diretoria aprova uma e recusa outra: a ficha passa a aguardar pagamento e pede ação
        var pedidos = ficha.GetProperty("categorias").EnumerateArray().ToDictionary(x => x.GetProperty("nome").GetString()!, x => x.GetProperty("id").GetGuid());
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedidos["Master 40+"]}/aprovar", new { })).Status.Should().Be(HttpStatusCode.NoContent);
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedidos["Hipermaster 60+"]}/recusar", new { motivo = "Fora da idade" })).Status.Should().Be(HttpStatusCode.NoContent);

        var depois = Dados((await Chamar(conta, HttpMethod.Get, "/api/participante/inscricoes")).Json);
        var f2 = depois.GetProperty("inscricoes").EnumerateArray().Single();
        f2.GetProperty("situacao").GetString().Should().Be("AguardandoPagamento");
        depois.GetProperty("acaoNecessaria").GetBoolean().Should().BeTrue();
        f2.GetProperty("categorias").EnumerateArray().Select(x => x.GetProperty("status").GetString()).Should().Equal("Recusada", "AguardandoPagamento");

        // a outra conta só enxerga a dela
        var dela = Dados((await Chamar(outra, HttpMethod.Get, "/api/participante/inscricoes")).Json);
        dela.GetProperty("inscricoes").EnumerateArray().Single().GetProperty("protocolo").GetString().Should().Be($"{c.Ano}-0002");
    }

    // ------------------------------------------------------------------ isolamento

    [Theory]
    [InlineData("Administrador")]
    [InlineData("AdministradorDeAssociacao")]
    [InlineData("Arbitro")]
    public async Task A_equipe_nao_usa_as_rotas_do_participante_e_sem_login_e_401(string perfil)
    {
        if (SemBanco) return;
        var equipe = _fabrica.ClienteCom(ApiFactory.Token(perfil));
        (await Chamar(equipe, HttpMethod.Post, "/api/participante/vinculo", Vinculo("12345678909"))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(equipe, HttpMethod.Get, $"/api/participante/temporadas/{Guid.NewGuid()}/inscricao/preparo")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(equipe, HttpMethod.Get, "/api/participante/inscricoes")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Post, "/api/participante/inscricoes", new { })).Status.Should().Be(HttpStatusCode.Unauthorized);
        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, "/api/participante/inscricoes")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }
}

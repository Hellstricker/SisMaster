using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApps.WebApi.Data;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G7 pela API de verdade: estornar pagamento, desfazer aprovação, reabrir pedido, corrigir dados, exportar CSV e importar
/// planilha, com auditoria e escopo por associação. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class InscricoesG7ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public InscricoesG7ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private HttpClient Admin() => _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static string CpfValido(int n)
    {
        var b = (100000000 + n * 7919 % 800000000).ToString()[..9].Select(c => c - '0').ToArray();
        int Dv(int[] a) { var s = a.Select((x, i) => x * (a.Length + 1 - i)).Sum(); var r = s * 10 % 11; return r == 10 ? 0 : r; }
        var d1 = Dv(b);
        var d2 = Dv([.. b, d1]);
        return string.Concat(b) + d1 + d2;
    }

    private static object Ficha(Guid temporada, Guid tc, string cpf, string nome, int altura = 180) => new
    {
        temporadaId = temporada, nome, cpf, nascimento = "1975-05-10", sexo = "Masculino", email = "atleta@exemplo.test",
        telefone = "71999990000", alturaCm = altura, pesoKg = 80, posicao = "Ala", consentimentoLgpd = true, temporadaCategoriaIds = new[] { tc },
    };

    private sealed record Cenario(Estrutura E, Guid Tc, HttpClient Admin);

    private async Task<Cenario> NovoAsync(string nome)
    {
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, nome);
        var tc = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}")).Corpo).RootElement
            .GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();
        await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/taxa-inscricao", new { taxa = 100 });
        return new Cenario(e, tc, admin);
    }

    private static async Task<JsonElement> FichaDe(HttpClient admin, Guid temporada, string nome) =>
        JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/inscricoes")).Corpo).RootElement
            .EnumerateArray().First(f => f.GetProperty("pessoa").GetProperty("nome").GetString() == nome).Clone();

    private async Task<(Guid Ficha, Guid Pedido)> Inscrever(Cenario c, string nome, int n, bool aprovar = false, bool taxa = false)
    {
        (await Chamar(c.Admin, HttpMethod.Post, "/api/inscricoes", Ficha(c.E.TemporadaId, c.Tc, CpfValido(n), nome))).Status.Should().Be(HttpStatusCode.NoContent);
        var f = await FichaDe(c.Admin, c.E.TemporadaId, nome);
        var ficha = f.GetProperty("id").GetGuid();
        var pedido = f.GetProperty("categorias")[0].GetProperty("id").GetGuid();
        if (aprovar) (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/aprovar", new { })).Status.Should().Be(HttpStatusCode.NoContent);
        if (taxa) (await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha}/pagamentos/taxa", new { dataPagamento = "2026-10-09" })).Status.Should().Be(HttpStatusCode.NoContent);
        return (ficha, pedido);
    }

    private async Task<List<(string Acao, string Detalhe)>> Auditoria(string acao)
    {
        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<CampeonatoDbContext>();
        return (await db.RegistrosDeAuditoria.AsNoTracking().Where(r => r.Acao == acao).ToListAsync()).Select(r => (r.Acao, r.Detalhe)).ToList();
    }

    // ------------------------------------------------------------------ estorno

    [Fact]
    public async Task Estornar_a_taxa_volta_o_pedido_para_aguardando_e_registra_a_auditoria()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EstornoG7");
        var (ficha, pedido) = await Inscrever(c, "Estorno Taxa Silva", 1, aprovar: true, taxa: true);
        var pagamento = (await FichaDe(c.Admin, c.E.TemporadaId, "Estorno Taxa Silva")).GetProperty("financeiro").GetProperty("pagamentos")[0].GetProperty("id").GetGuid();

        var sem = await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha}/pagamentos/{pagamento}/estornar", new { motivo = "" });
        sem.Status.Should().Be(HttpStatusCode.BadRequest);

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha}/pagamentos/{pagamento}/estornar", new { motivo = "lançada na ficha errada" }))
            .Status.Should().Be(HttpStatusCode.NoContent);

        var depois = await FichaDe(c.Admin, c.E.TemporadaId, "Estorno Taxa Silva");
        depois.GetProperty("financeiro").GetProperty("taxaPaga").GetBoolean().Should().BeFalse();
        depois.GetProperty("categorias")[0].GetProperty("status").GetString().Should().Be("AguardandoPagamento");
        var lanc = depois.GetProperty("financeiro").GetProperty("pagamentos")[0];
        lanc.GetProperty("estornado").GetBoolean().Should().BeTrue();
        lanc.GetProperty("motivoEstorno").GetString().Should().Be("lançada na ficha errada");
        (await Auditoria("EstornoDePagamento")).Should().Contain(a => a.Detalhe.Contains("lançada na ficha errada"));

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha}/pagamentos/{pagamento}/estornar", new { motivo = "de novo" }))
            .Status.Should().Be(HttpStatusCode.BadRequest, "o mesmo pagamento não é estornado duas vezes");
        _ = pedido;
    }

    [Fact]
    public async Task Nao_estorna_a_taxa_de_quem_esta_em_equipe()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EstornoEquipeG7");
        var (ficha, pedido) = await Inscrever(c, "Em Equipe Santos", 2, aprovar: true, taxa: true);
        var equipe = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/equipes")).Corpo).RootElement;
        var cat = (equipe.TryGetProperty("data", out var d) ? d : equipe).GetProperty("categorias")[0];
        var equipeId = cat.GetProperty("equipes")[0].GetProperty("id").GetGuid();
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/equipes/{equipeId}/atletas", new { inscricaoCategoriaId = pedido })).Status.Should().Be(HttpStatusCode.NoContent);
        var pagamento = (await FichaDe(c.Admin, c.E.TemporadaId, "Em Equipe Santos")).GetProperty("financeiro").GetProperty("pagamentos")[0].GetProperty("id").GetGuid();

        var estorno = await Chamar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha}/pagamentos/{pagamento}/estornar", new { motivo = "teste" });
        var desfazer = await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/desfazer-aprovacao", new { motivo = "teste" });

        estorno.Status.Should().Be(HttpStatusCode.BadRequest);
        estorno.Corpo.Should().Contain("está em uma equipe");
        desfazer.Status.Should().Be(HttpStatusCode.BadRequest);
        desfazer.Corpo.Should().Contain("está em uma equipe");
    }

    // ------------------------------------------------------------------ desfazer / reabrir

    [Fact]
    public async Task Desfazer_aprovacao_exige_motivo_volta_para_pendente_e_registra()
    {
        if (SemBanco) return;
        var c = await NovoAsync("DesfazerG7");
        var (_, pedido) = await Inscrever(c, "Aprovado Engano", 3, aprovar: true);

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/desfazer-aprovacao", new { motivo = "" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/desfazer-aprovacao", new { motivo = "aprovado por engano" })).Status.Should().Be(HttpStatusCode.NoContent);

        (await FichaDe(c.Admin, c.E.TemporadaId, "Aprovado Engano")).GetProperty("categorias")[0].GetProperty("status").GetString().Should().Be("Pendente");
        (await Auditoria("DesfazerAprovacao")).Should().Contain(a => a.Detalhe.Contains("aprovado por engano"));
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/desfazer-aprovacao", new { motivo = "x" })).Status.Should().Be(HttpStatusCode.BadRequest, "só pedido aprovado");
    }

    [Fact]
    public async Task Reabrir_pedido_recusado_volta_para_pendente_e_nao_duplica_pedido_ativo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ReabrirG7");
        var (_, pedido) = await Inscrever(c, "Recusado Engano", 4);
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/reabrir")).Status.Should().Be(HttpStatusCode.BadRequest, "só pedido recusado");
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/recusar", new { motivo = "documento ilegível" })).Status.Should().Be(HttpStatusCode.NoContent);

        // Nova ficha da mesma pessoa na mesma categoria (permitida porque a anterior foi recusada) impede reabrir a antiga.
        (await Chamar(c.Admin, HttpMethod.Post, "/api/inscricoes", Ficha(c.E.TemporadaId, c.Tc, CpfValido(4), "Recusado Engano"))).Status.Should().Be(HttpStatusCode.NoContent);
        var duplicado = await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/reabrir");
        duplicado.Status.Should().Be(HttpStatusCode.BadRequest);
        duplicado.Corpo.Should().Contain("outro pedido ativo");

        var c2 = await NovoAsync("ReabrirOkG7");
        var (_, pedido2) = await Inscrever(c2, "Recusado Certo", 5);
        await Chamar(c2.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido2}/recusar", new { motivo = "fora da idade" });
        (await Chamar(c2.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido2}/reabrir")).Status.Should().Be(HttpStatusCode.NoContent);
        (await FichaDe(c2.Admin, c2.E.TemporadaId, "Recusado Certo")).GetProperty("categorias")[0].GetProperty("status").GetString().Should().Be("Pendente");
        (await Auditoria("ReabrirPedido")).Should().Contain(a => a.Detalhe.Contains("fora da idade"));
    }

    // ------------------------------------------------------------------ correções

    [Fact]
    public async Task Corrigir_dados_da_ficha_altera_ficha_e_contato_e_valida()
    {
        if (SemBanco) return;
        var c = await NovoAsync("DadosG7");
        var (ficha, _) = await Inscrever(c, "Dados Corrigidos", 6);

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes/{ficha}/dados", new { alturaCm = 50, email = "x@y.test" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/inscricoes/{ficha}/dados",
            new { alturaCm = 190, pesoKg = 92, posicao = "Pivô", possuiPlanoSaude = true, nomePlanoSaude = "Unimed", email = "novo@exemplo.test", telefone = "71988880000" }))
            .Status.Should().Be(HttpStatusCode.NoContent);

        var f = await FichaDe(c.Admin, c.E.TemporadaId, "Dados Corrigidos");
        f.GetProperty("dados").GetProperty("alturaCm").GetInt32().Should().Be(190);
        f.GetProperty("dados").GetProperty("nomePlanoSaude").GetString().Should().Be("Unimed");
        f.GetProperty("pessoa").GetProperty("email").GetString().Should().Be("novo@exemplo.test");
        var log = (await Auditoria("CorrecaoDeFicha")).Single(a => a.Detalhe.Contains("altura"));
        log.Detalhe.Should().NotContain("novo@exemplo.test", "a auditoria diz o que mudou, não guarda o dado pessoal");
    }

    [Fact]
    public async Task So_o_administrador_corrige_a_identificacao_e_o_cpf_aparece_mascarado_na_auditoria()
    {
        if (SemBanco) return;
        var c = await NovoAsync("IdentG7");
        await Inscrever(c, "Identificacao Antiga", 7);
        var pessoa = (await FichaDe(c.Admin, c.E.TemporadaId, "Identificacao Antiga")).GetProperty("pessoa").GetProperty("id").GetGuid();
        var corpo = new { nome = "Identificacao Nova", cpf = CpfValido(77), nascimento = "1974-01-02", sexo = "Masculino" };

        var gestor = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", c.E.AssociacaoId));
        (await Chamar(gestor, HttpMethod.Patch, $"/api/pessoas/{pessoa}/identificacao", corpo)).Status.Should().Be(HttpStatusCode.Forbidden);

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/pessoas/{pessoa}/identificacao", corpo)).Status.Should().Be(HttpStatusCode.NoContent);
        var f = await FichaDe(c.Admin, c.E.TemporadaId, "Identificacao Nova");
        f.GetProperty("pessoa").GetProperty("cpf").GetString().Should().Contain(CpfValido(77)[..3]);
        var log = (await Auditoria("CorrecaoDeIdentificacao")).Single(a => a.Detalhe.Contains("Identificacao Nova"));
        log.Detalhe.Should().Contain("***.").And.NotContain(CpfValido(7), "o CPF nunca vai inteiro para a auditoria");
    }

    [Fact]
    public async Task Corrigir_identificacao_recusa_cpf_de_outra_pessoa()
    {
        if (SemBanco) return;
        var c = await NovoAsync("CpfDuplG7");
        await Inscrever(c, "Pessoa Um", 8);
        await Inscrever(c, "Pessoa Dois", 9);
        var um = (await FichaDe(c.Admin, c.E.TemporadaId, "Pessoa Um")).GetProperty("pessoa").GetProperty("id").GetGuid();

        var r = await Chamar(c.Admin, HttpMethod.Patch, $"/api/pessoas/{um}/identificacao",
            new { nome = "Pessoa Um", cpf = CpfValido(9), nascimento = "1975-05-10", sexo = "Masculino" });

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("Pessoa Dois");
    }

    [Fact]
    public async Task Administrador_de_outra_associacao_nao_mexe_nas_inscricoes()
    {
        if (SemBanco) return;
        var c = await NovoAsync("EscopoG7");
        var (ficha, pedido) = await Inscrever(c, "Escopo Teste", 10, aprovar: true);
        var outro = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        (await Chamar(outro, HttpMethod.Patch, $"/api/inscricoes/{ficha}/dados", new { email = "a@b.test" })).Status.Should().NotBe(HttpStatusCode.NoContent);
        (await Chamar(outro, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/desfazer-aprovacao", new { motivo = "x" })).Status.Should().NotBe(HttpStatusCode.NoContent);
        (await Chamar(outro, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/exportar")).Status.Should().NotBe(HttpStatusCode.OK);
        (await Chamar(outro, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao/previa", new { csv = "nome" })).Status.Should().NotBe(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------ exportar

    [Fact]
    public async Task Exportar_nao_leva_cpf_nem_contato_por_padrao_respeita_o_filtro_e_registra()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ExportG7");
        await Inscrever(c, "Exporta Pendente", 11);
        await Inscrever(c, "Exporta Aprovado", 12, aprovar: true);
        await Inscrever(c, "=SOMA(1;1)", 13);

        var resp = await c.Admin.GetAsync($"/api/temporadas/{c.E.TemporadaId}/inscricoes/exportar");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var csv = Encoding.UTF8.GetString(bytes);
        csv.Should().Contain("Nome;Idade;Perfil;Altura (cm);Peso (kg);Posição;Categoria;Situação").And.Contain("Exporta Pendente").And.Contain("Exporta Aprovado");
        csv.Should().NotContain(CpfValido(11)[..3] + ".", "sem a coluna CPF").And.NotContain("atleta@exemplo.test").And.NotContain("E-mail");
        csv.Should().Contain("'=SOMA", "texto que começa com = é neutralizado para não virar fórmula no Excel");

        var comTudo = Encoding.UTF8.GetString(await (await c.Admin.GetAsync($"/api/temporadas/{c.E.TemporadaId}/inscricoes/exportar?cpf=true&contato=true")).Content.ReadAsByteArrayAsync());
        comTudo.Should().Contain("CPF").And.Contain("E-mail;Telefone").And.Contain("atleta@exemplo.test");

        var soAprovados = Encoding.UTF8.GetString(await (await c.Admin.GetAsync($"/api/temporadas/{c.E.TemporadaId}/inscricoes/exportar?status=AguardandoPagamento")).Content.ReadAsByteArrayAsync());
        soAprovados.Should().Contain("Exporta Aprovado").And.NotContain("Exporta Pendente");

        var log = (await Auditoria("ExportacaoDeInscricoes")).Where(a => a.Detalhe.Contains("CPF")).ToList();
        log.Should().NotBeEmpty("a exportação com CPF fica registrada");
    }

    // ------------------------------------------------------------------ importar

    [Fact]
    public async Task Importar_confere_antes_cria_so_o_que_esta_certo_e_nao_duplica()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ImportG7");
        await Inscrever(c, "Ja Inscrito Lima", 20);
        var cat = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}")).Corpo).RootElement
            .GetProperty("categorias")[0].GetProperty("nome").GetString();

        var csv = string.Join("\n",
            "nome;cpf;nascimento;sexo;e-mail;telefone;categoria;altura;peso;posição",
            $"Novo Atleta Um;{CpfValido(21)};10/05/1975;M;um@exemplo.test;71999990001;{cat};178;82;Ala",
            $"Ja Inscrito Lima;{CpfValido(20)};10/05/1975;M;ja@exemplo.test;;{cat};;;",
            "Cpf Ruim;123.456.789-00;10/05/1975;M;ruim@exemplo.test;;" + cat + ";;;",
            $"Categoria Errada;{CpfValido(22)};10/05/1975;M;cat@exemplo.test;;Inexistente;;;",
            $"Novo Atleta Um Repetido;{CpfValido(21)};10/05/1975;M;rep@exemplo.test;;{cat};;;",
            $"Novo Mais Novo;{CpfValido(23)};10/05/1995;M;novo@exemplo.test;;{cat};;;");

        var previa = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao/previa", new { csv });
        previa.Status.Should().Be(HttpStatusCode.OK, previa.Corpo);
        var p = JsonDocument.Parse(previa.Corpo).RootElement;
        var resultados = p.GetProperty("linhas").EnumerateArray().ToDictionary(l => l.GetProperty("linha").GetInt32(), l => l.GetProperty("resultado").GetString());
        resultados[2].Should().Be("Criar");
        resultados[3].Should().Be("Ignorar");
        resultados[4].Should().Be("Erro");
        resultados[5].Should().Be("Erro");
        resultados[6].Should().Be("Erro");
        resultados[7].Should().Be("Criar");
        var repetida = p.GetProperty("linhas").EnumerateArray().First(l => l.GetProperty("nome").GetString() == "Novo Atleta Um Repetido");
        repetida.GetProperty("mensagens")[0].GetString().Should().Contain("repetido no arquivo");
        var fora = p.GetProperty("linhas").EnumerateArray().First(l => l.GetProperty("nome").GetString() == "Novo Mais Novo");
        fora.GetProperty("avisos").GetArrayLength().Should().BeGreaterThan(0, "31 anos em categoria 40+ é fora do esperado");

        // a prévia não grava nada
        JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/inscricoes")).Corpo).RootElement.GetArrayLength().Should().Be(1);

        var semConsentimento = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao", new { csv, consentimentoConfirmado = false });
        semConsentimento.Status.Should().Be(HttpStatusCode.BadRequest);
        semConsentimento.Corpo.Should().Contain("consentimento");

        var feito = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao", new { csv, consentimentoConfirmado = true });
        feito.Status.Should().Be(HttpStatusCode.OK, feito.Corpo);
        var r = JsonDocument.Parse(feito.Corpo).RootElement;
        r.GetProperty("criadas").GetInt32().Should().Be(2);
        r.GetProperty("comErro").GetInt32().Should().Be(3);
        JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/inscricoes")).Corpo).RootElement.GetArrayLength().Should().Be(3);
        (await Auditoria("ImportacaoDeInscricoes")).Should().Contain(a => a.Detalhe.Contains("2 fichas criadas"));

        // rodar de novo não duplica
        var outra = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao", new { csv, consentimentoConfirmado = true })).Corpo).RootElement;
        outra.GetProperty("criadas").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Importar_recusa_planilha_sem_as_colunas_obrigatorias_ou_vazia()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ImportRuimG7");

        var semColuna = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao/previa", new { csv = "nome;cpf\nFulano;123" });
        semColuna.Status.Should().Be(HttpStatusCode.BadRequest);
        semColuna.Corpo.Should().Contain("Falta a coluna");
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/inscricoes/importacao/previa", new { csv = "" })).Status.Should().Be(HttpStatusCode.BadRequest);
    }
}

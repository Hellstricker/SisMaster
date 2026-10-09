using System.Globalization;
using System.Text;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Importacao;

public enum ResultadoDaLinha { Criar, Ignorar, Erro }

public sealed record LinhaDaImportacao(int Linha, string Nome, ResultadoDaLinha Resultado, IReadOnlyList<string> Mensagens, IReadOnlyList<string> Avisos);

public sealed record PreviaDaImportacao(int Total, int AContar, int Ignoradas, int ComErro, IReadOnlyList<LinhaDaImportacao> Linhas);

public sealed record ResultadoDaImportacao(int Criadas, int Ignoradas, int ComErro, IReadOnlyList<LinhaDaImportacao> Linhas);

/// <summary>
/// Importa inscrições de uma planilha CSV. A prévia confere tudo sem gravar nada; a importação só cria as linhas
/// sem erro. Colunas: nome, cpf, nascimento, sexo, e-mail, telefone, categoria (várias separadas por |), altura, peso, posição.
/// </summary>
public class ImportacaoDeInscricoes(
    ITemporadaRepository temporadas, IInscricaoRepository fichas, IPessoaRepository pessoas, IMediatorHandler mediator, IAuditoria auditoria)
{
    public const int MaximoDeLinhas = 500;
    public const int MaximoDeCaracteres = 1_000_000;

    public async Task<PreviaDaImportacao?> PreviaAsync(Guid temporadaId, string csv, CancellationToken ct = default)
    {
        var temporada = await temporadas.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return null;
        temporada.ValidarAceitaInscricaoDaDiretoria();
        var (linhas, _) = await Analisar(temporada, csv, ct);
        return new PreviaDaImportacao(linhas.Count, linhas.Count(l => l.Linha.Resultado == ResultadoDaLinha.Criar),
            linhas.Count(l => l.Linha.Resultado == ResultadoDaLinha.Ignorar), linhas.Count(l => l.Linha.Resultado == ResultadoDaLinha.Erro),
            linhas.Select(l => l.Linha).ToList());
    }

    public async Task<ResultadoDaImportacao?> ImportarAsync(Guid temporadaId, string csv, CancellationToken ct = default)
    {
        var temporada = await temporadas.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return null;
        temporada.ValidarAceitaInscricaoDaDiretoria();
        var (linhas, _) = await Analisar(temporada, csv, ct);

        var finais = new List<LinhaDaImportacao>();
        var criadas = 0;
        foreach (var item in linhas)
        {
            if (item.Linha.Resultado != ResultadoDaLinha.Criar || item.Comando is null) { finais.Add(item.Linha); continue; }

            var resultado = await mediator.EnviarComando(item.Comando);
            if (resultado.IsValid) { criadas++; finais.Add(item.Linha); }
            else finais.Add(item.Linha with { Resultado = ResultadoDaLinha.Erro, Mensagens = resultado.Errors.Select(e => e.ErrorMessage).ToList() });
        }

        var erros = finais.Count(l => l.Resultado == ResultadoDaLinha.Erro);
        var ignoradas = finais.Count(l => l.Resultado == ResultadoDaLinha.Ignorar);
        await auditoria.RegistrarEGravarAsync("ImportacaoDeInscricoes", "Temporada", temporadaId,
            $"{criadas} fichas criadas, {ignoradas} ignoradas, {erros} com erro (de {finais.Count} linhas).", ct);
        return new ResultadoDaImportacao(criadas, ignoradas, erros, finais);
    }

    private sealed record Analisada(LinhaDaImportacao Linha, EnviarInscricaoCommand? Comando);

    private async Task<(List<Analisada>, string[])> Analisar(Temporada temporada, string csv, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(csv)) throw new Domain.Excecoes.ImportacaoInvalida("O arquivo está vazio.");
        if (csv.Length > MaximoDeCaracteres) throw new Domain.Excecoes.ImportacaoInvalida("O arquivo é grande demais (máximo de 1 MB).");

        var tabela = LerCsv(csv);
        if (tabela.Count < 2) throw new Domain.Excecoes.ImportacaoInvalida("O arquivo precisa ter o cabeçalho e ao menos uma linha de dados.");
        if (tabela.Count - 1 > MaximoDeLinhas) throw new Domain.Excecoes.ImportacaoInvalida($"Máximo de {MaximoDeLinhas} linhas por importação.");

        var colunas = MapearColunas(tabela[0]);
        foreach (var obrigatoria in new[] { "nome", "cpf", "nascimento", "sexo", "email", "categoria" })
            if (!colunas.ContainsKey(obrigatoria))
                throw new Domain.Excecoes.ImportacaoInvalida($"Falta a coluna \"{obrigatoria}\" no cabeçalho.");

        var categorias = temporada.Categorias.ToList();
        var vistos = new HashSet<string>();
        var resultado = new List<Analisada>();

        for (var i = 1; i < tabela.Count; i++)
        {
            var celulas = tabela[i];
            string Valor(string coluna) => colunas.TryGetValue(coluna, out var idx) && idx < celulas.Count ? celulas[idx].Trim() : string.Empty;

            var numero = i + 1; // linha da planilha (o cabeçalho é a 1)
            var nome = Valor("nome");
            var erros = new List<string>();
            var avisos = new List<string>();

            if (nome.Length < 3 || nome.Length > 150) erros.Add("Nome deve ter entre 3 e 150 caracteres.");

            var cpf = Domain.Participantes.Cpf.RemoverFormatacao(Valor("cpf"));
            if (!CpfValido(cpf)) erros.Add("CPF inválido.");

            if (!TentarData(Valor("nascimento"), out var nascimento)) erros.Add("Data de nascimento inválida (use DD/MM/AAAA).");
            else if (nascimento > DateOnly.FromDateTime(DateTime.Today) || nascimento.Year < 1900) erros.Add("Data de nascimento inválida.");

            Sexo sexo = default;
            var sexoTxt = Normalizar(Valor("sexo"));
            if (sexoTxt is "m" or "masculino") sexo = Sexo.Masculino;
            else if (sexoTxt is "f" or "feminino") sexo = Sexo.Feminino;
            else erros.Add("Sexo deve ser Masculino ou Feminino.");

            var email = Valor("email");
            if (email.Length < 5 || email.Length > 150 || !email.Contains('@')) erros.Add("E-mail inválido.");

            int? altura = null, peso = null;
            if (Valor("altura") != "" && !TentarInteiro(Valor("altura"), 100, 260, out altura)) erros.Add("Altura deve estar entre 100 e 260 cm.");
            if (Valor("peso") != "" && !TentarInteiro(Valor("peso"), 30, 250, out peso)) erros.Add("Peso deve estar entre 30 e 250 kg.");

            var pedidas = new List<TemporadaCategoria>();
            var nomesPedidos = Valor("categoria").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (nomesPedidos.Length == 0) erros.Add("Informe ao menos uma categoria.");
            foreach (var nomeCategoria in nomesPedidos)
            {
                var achada = categorias.FirstOrDefault(c => Normalizar(c.Nome) == Normalizar(nomeCategoria));
                if (achada is null) erros.Add($"Categoria \"{nomeCategoria}\" não existe nesta temporada (existem: {string.Join(", ", categorias.Select(c => c.Nome))}).");
                else if (!pedidas.Contains(achada)) pedidas.Add(achada);
            }

            // Repetição dentro do arquivo
            if (erros.Count == 0 && !vistos.Add(cpf)) erros.Add("CPF repetido no arquivo: use uma linha por pessoa (várias categorias separadas por |).");

            // Já inscrita?
            var ja = new List<TemporadaCategoria>();
            if (erros.Count == 0)
            {
                var existente = await pessoas.ObterPorCpfAsync(cpf, ct);
                if (existente is not null)
                    foreach (var tc in pedidas)
                        if (await fichas.ExisteAtivaAsync(existente.Id, tc.Id, ct)) ja.Add(tc);
            }

            if (erros.Count > 0)
            {
                resultado.Add(new Analisada(new LinhaDaImportacao(numero, nome, ResultadoDaLinha.Erro, erros, avisos), null));
                continue;
            }

            var novas = pedidas.Except(ja).ToList();
            if (novas.Count == 0)
            {
                resultado.Add(new Analisada(new LinhaDaImportacao(numero, nome, ResultadoDaLinha.Ignorar,
                    [$"Já inscrita em {string.Join(", ", ja.Select(c => c.Nome))}."], avisos), null));
                continue;
            }

            if (ja.Count > 0) avisos.Add($"Já inscrita em {string.Join(", ", ja.Select(c => c.Nome))}: entra só em {string.Join(", ", novas.Select(c => c.Nome))}.");
            foreach (var tc in novas)
                foreach (var fora in tc.DescreverExcecoes(nascimento, sexo, temporada.Ano))
                    avisos.Add($"{tc.Nome}: {fora}. Entra como pedido pendente para a diretoria decidir.");

            var comando = new EnviarInscricaoCommand
            {
                TemporadaId = temporada.Id, PelaDiretoria = true, Nome = nome, Cpf = cpf, Nascimento = nascimento, Sexo = sexo,
                Email = email, Telefone = Valor("telefone") is { Length: > 0 } t ? t : null, AlturaCm = altura, PesoKg = peso,
                Posicao = Valor("posicao") is { Length: > 0 } po ? po : null, ConsentimentoLgpd = true,
                TemporadaCategoriaIds = novas.Select(c => c.Id).ToList(),
            };
            resultado.Add(new Analisada(new LinhaDaImportacao(numero, nome, ResultadoDaLinha.Criar, [], avisos), comando));
        }
        return (resultado, []);
    }

    // ---------------------------------------------------------------- leitura do CSV

    private static List<List<string>> LerCsv(string texto)
    {
        texto = texto.TrimStart('﻿');
        var primeira = texto.Split('\n', 2)[0];
        var sep = new[] { ';', ',', '\t' }.OrderByDescending(c => primeira.Count(x => x == c)).First();

        var linhas = new List<List<string>>();
        var linha = new List<string>();
        var celula = new StringBuilder();
        var aspas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (aspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { celula.Append('"'); i++; }
                else if (c == '"') aspas = false;
                else celula.Append(c);
            }
            else if (c == '"') aspas = true;
            else if (c == sep) { linha.Add(celula.ToString()); celula.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                linha.Add(celula.ToString()); celula.Clear();
                if (linha.Any(x => x.Trim().Length > 0)) linhas.Add(linha);
                linha = [];
            }
            else celula.Append(c);
        }
        linha.Add(celula.ToString());
        if (linha.Any(x => x.Trim().Length > 0)) linhas.Add(linha);
        return linhas;
    }

    private static Dictionary<string, int> MapearColunas(List<string> cabecalho)
    {
        var apelidos = new Dictionary<string, string>
        {
            ["nome"] = "nome", ["nomecompleto"] = "nome", ["cpf"] = "cpf",
            ["nascimento"] = "nascimento", ["datanascimento"] = "nascimento", ["datadenascimento"] = "nascimento",
            ["sexo"] = "sexo", ["email"] = "email", ["telefone"] = "telefone", ["celular"] = "telefone", ["fone"] = "telefone",
            ["categoria"] = "categoria", ["categorias"] = "categoria",
            ["altura"] = "altura", ["alturacm"] = "altura", ["peso"] = "peso", ["pesokg"] = "peso", ["posicao"] = "posicao",
        };
        var mapa = new Dictionary<string, int>();
        for (var i = 0; i < cabecalho.Count; i++)
        {
            var chave = new string(Normalizar(cabecalho[i]).Where(char.IsLetterOrDigit).ToArray());
            if (apelidos.TryGetValue(chave, out var coluna) && !mapa.ContainsKey(coluna)) mapa[coluna] = i;
        }
        return mapa;
    }

    // ---------------------------------------------------------------- validações

    private static string Normalizar(string s)
    {
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static bool TentarData(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    private static bool TentarInteiro(string texto, int minimo, int maximo, out int? valor)
    {
        valor = null;
        if (!int.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < minimo || n > maximo) return false;
        valor = n;
        return true;
    }

    /// <summary>CPF com os dois dígitos verificadores corretos (e não todos iguais).</summary>
    public static bool CpfValido(string cpf)
    {
        if (cpf.Length != 11 || !cpf.All(char.IsDigit) || cpf.Distinct().Count() == 1) return false;
        int Digito(int tamanho)
        {
            var soma = 0;
            for (var i = 0; i < tamanho; i++) soma += (cpf[i] - '0') * (tamanho + 1 - i);
            var r = soma * 10 % 11;
            return r == 10 ? 0 : r;
        }
        return Digito(9) == cpf[9] - '0' && Digito(10) == cpf[10] - '0';
    }
}

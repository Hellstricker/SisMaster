using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Excecoes;

/// <summary>Planilha que não dá nem para ler (vazia, grande demais, sem as colunas obrigatórias): vira mensagem para a tela.</summary>
public class ImportacaoInvalida(string mensagem) : DomainException(mensagem);

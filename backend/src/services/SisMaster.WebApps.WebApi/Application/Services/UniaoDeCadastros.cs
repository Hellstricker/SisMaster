using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// Une dois cadastros da mesma pessoa (digitada duas vezes, ou conta do site criada com outro CPF): as fichas do cadastro duplicado passam
/// para o principal e o duplicado é apagado. Só o Administrador. Recusa quando haveria dois pedidos ativos na mesma categoria ou duas
/// contas diferentes. Nome, CPF e demais dados do principal nunca mudam. Fica na auditoria (CPFs mascarados).
/// </summary>
public class UniaoDeCadastros(IPessoaRepository pessoas, IInscricaoRepository inscricoes, IAuditoria auditoria)
{
    private static string Mascarar(string cpf) => cpf.Length == 11 ? $"***.{cpf[3..6]}.{cpf[6..9]}-**" : "***";

    public async Task UnirAsync(Guid principalId, string cpfDaDuplicada, CancellationToken ct)
    {
        var principal = await pessoas.ObterPorIdAsync(principalId, ct) ?? throw new DomainException("Cadastro não encontrado");
        var cpf = Cpf.RemoverFormatacao(cpfDaDuplicada ?? string.Empty);
        if (cpf.Length != 11) throw new DomainException("Informe o CPF completo do cadastro duplicado");
        var duplicada = await pessoas.ObterPorCpfAsync(cpf, ct) ?? throw new DomainException("Não há cadastro com esse CPF");
        if (duplicada.Id == principal.Id) throw new DomainException("O cadastro duplicado precisa ser outro, com CPF diferente do principal");
        if (duplicada.ContaId is not null && principal.ContaId is not null)
            throw new DomainException("Os dois cadastros estão ligados a contas do site diferentes. Resolva a conta antes de unir");

        var fichas = await inscricoes.ListarParaUniaoAsync(duplicada.Id, ct);
        foreach (var pedido in fichas.SelectMany(f => f.Categorias).Where(c => c.Ativa))
            if (await inscricoes.ExisteAtivaAsync(principal.Id, pedido.TemporadaCategoriaId, ct))
                throw new DomainException($"{principal.Nome} já tem um pedido ativo em uma das categorias do cadastro duplicado. Recuse ou desfaça um deles antes de unir");

        var conta = duplicada.DesligarConta();
        foreach (var ficha in fichas) ficha.ReatribuirPara(principal.Id);
        if (duplicada.Perfil == PerfilPessoa.Associado) principal.TornarAssociado();

        auditoria.Registrar("UniaoDeCadastros", "Pessoa", principal.Id,
            $"Cadastro de {duplicada.Nome} (CPF {Mascarar(duplicada.Cpf.Numero)}) unido a {principal.Nome} (CPF {Mascarar(principal.Cpf.Numero)}): {fichas.Count} ficha(s) passaram para o principal" +
            (conta is not null ? "; a conta do site passou para o principal" : string.Empty));

        // Em duas gravações para respeitar o índice único da conta: primeiro as fichas passam e a conta é solta, depois a conta chega ao principal.
        await inscricoes.UnitOfWork.Commit();
        if (conta is not null) principal.VincularConta(conta);
        pessoas.Remover(duplicada);
        await pessoas.UnitOfWork.Commit();
    }
}

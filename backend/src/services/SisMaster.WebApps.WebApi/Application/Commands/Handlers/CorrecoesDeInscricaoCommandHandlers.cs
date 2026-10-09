using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

internal static class Falha
{
    public static ValidationResult Com(ValidationResult resultado, string mensagem)
    {
        resultado.Errors.Add(new ValidationFailure(string.Empty, mensagem));
        return resultado;
    }

    /// <summary>"***.456.789-**": o registro de auditoria nunca guarda o CPF inteiro.</summary>
    public static string Mascarar(string cpfSomenteDigitos) =>
        cpfSomenteDigitos.Length == 11 ? $"***.{cpfSomenteDigitos[3..6]}.{cpfSomenteDigitos[6..9]}-**" : "***";
}

public class EditarDadosFichaCommandHandler(
    IInscricaoRepository fichas, ITemporadaRepository temporadas, IAuditoria auditoria)
    : IRequestHandler<EditarDadosFichaCommand, ValidationResult>
{
    public async Task<ValidationResult> Handle(EditarDadosFichaCommand request, CancellationToken ct)
    {
        if (!request.EhValido()) return request.ValidationResult;
        try
        {
            var ficha = await fichas.ObterPorIdAsync(request.InscricaoId, ct);
            if (ficha is null) return Falha.Com(request.ValidationResult, "Inscrição não encontrada");
            (await temporadas.ObterPorIdAsync(ficha.TemporadaId, ct))!.ValidarPermiteAlterarInscricoes();

            var antes = (ficha.AlturaCm, ficha.PesoKg, ficha.Posicao, ficha.PossuiPlanoSaude, ficha.NomePlanoSaude);
            var emailAntes = ficha.Pessoa.Email;
            var telefoneAntes = ficha.Pessoa.Telefone;

            ficha.AtualizarDados(request.AlturaCm, request.PesoKg, request.Posicao, request.PossuiPlanoSaude, request.NomePlanoSaude);
            ficha.Pessoa.AtualizarContato(request.Email, request.Telefone);

            var mudou = new List<string>();
            if (antes.AlturaCm != ficha.AlturaCm) mudou.Add("altura");
            if (antes.PesoKg != ficha.PesoKg) mudou.Add("peso");
            if (antes.Posicao != ficha.Posicao) mudou.Add("posição");
            if (antes.PossuiPlanoSaude != ficha.PossuiPlanoSaude || antes.NomePlanoSaude != ficha.NomePlanoSaude) mudou.Add("plano de saúde");
            if (emailAntes != ficha.Pessoa.Email) mudou.Add("e-mail");
            if (telefoneAntes != ficha.Pessoa.Telefone) mudou.Add("telefone");
            if (mudou.Count > 0)
                auditoria.Registrar("CorrecaoDeFicha", "Inscricao", ficha.Id, $"Campos corrigidos: {string.Join(", ", mudou)}");

            await fichas.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex) { return Falha.Com(request.ValidationResult, ex.Message); }
    }
}

public class CorrigirIdentificacaoCommandHandler(IPessoaRepository pessoas, IAuditoria auditoria)
    : IRequestHandler<CorrigirIdentificacaoCommand, ValidationResult>
{
    public async Task<ValidationResult> Handle(CorrigirIdentificacaoCommand request, CancellationToken ct)
    {
        if (!request.EhValido()) return request.ValidationResult;
        try
        {
            var pessoa = await pessoas.ObterPorIdAsync(request.PessoaId, ct);
            if (pessoa is null) return Falha.Com(request.ValidationResult, "Pessoa não encontrada");

            var cpfNovo = Cpf.RemoverFormatacao(request.Cpf);
            var cpfAntes = pessoa.Cpf.Numero;
            if (cpfNovo != cpfAntes)
            {
                var outra = await pessoas.ObterPorCpfAsync(cpfNovo, ct);
                if (outra is not null && outra.Id != pessoa.Id)
                    return Falha.Com(request.ValidationResult, $"Já existe outra pessoa cadastrada com este CPF ({outra.Nome})");
            }

            var detalhe = new List<string>();
            var nomeAntes = pessoa.Nome;
            var nascAntes = pessoa.Nascimento;
            var sexoAntes = pessoa.Sexo;

            pessoa.CorrigirIdentificacao(request.Nome, request.Cpf, request.Nascimento, request.Sexo);

            if (nomeAntes != pessoa.Nome) detalhe.Add($"nome: {nomeAntes} → {pessoa.Nome}");
            if (cpfAntes != pessoa.Cpf.Numero) detalhe.Add($"CPF: {Falha.Mascarar(cpfAntes)} → {Falha.Mascarar(pessoa.Cpf.Numero)}");
            if (nascAntes != pessoa.Nascimento) detalhe.Add($"nascimento: {nascAntes:dd/MM/yyyy} → {pessoa.Nascimento:dd/MM/yyyy}");
            if (sexoAntes != pessoa.Sexo) detalhe.Add($"sexo: {sexoAntes} → {pessoa.Sexo}");
            if (detalhe.Count > 0)
                auditoria.Registrar("CorrecaoDeIdentificacao", "Pessoa", pessoa.Id, string.Join("; ", detalhe));

            await pessoas.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex) { return Falha.Com(request.ValidationResult, ex.Message); }
    }
}

public class EstornarPagamentoCommandHandler(
    IInscricaoRepository fichas, ITemporadaRepository temporadas, IEquipeRepository equipes, IAuditoria auditoria,
    SisMaster.WebApi.Core.Usuarios.IAcessoDoUsuario acesso)
    : IRequestHandler<EstornarPagamentoCommand, ValidationResult>
{
    public async Task<ValidationResult> Handle(EstornarPagamentoCommand request, CancellationToken ct)
    {
        if (!request.EhValido()) return request.ValidationResult;
        try
        {
            var ficha = await fichas.ObterPorIdAsync(request.InscricaoId, ct);
            if (ficha is null) return Falha.Com(request.ValidationResult, "Inscrição não encontrada");
            (await temporadas.ObterPorIdAsync(ficha.TemporadaId, ct))!.ValidarPermiteAlterarInscricoes();

            var pagamento = ficha.Pagamentos.FirstOrDefault(p => p.Id == request.PagamentoId);
            if (pagamento is { Ativo: true, Tipo: TipoPagamento.Taxa })
            {
                // Estornar a taxa tira a efetivação: quem já está em equipe precisa sair dela antes.
                foreach (var pedido in ficha.Categorias.Where(c => c.Status == StatusInscricaoCategoria.Efetivada))
                    if (await equipes.AtletaExisteParaAsync(pedido.Id, ct))
                        return Falha.Com(request.ValidationResult,
                            $"{ficha.Pessoa.Nome} está em uma equipe. Retire-o da equipe antes de estornar a taxa");
            }

            var estornado = ficha.EstornarPagamento(request.PagamentoId, request.Motivo, acesso.Id);
            auditoria.Registrar("EstornoDePagamento", "Inscricao", ficha.Id,
                $"{estornado.Tipo} de {estornado.Valor:N2} (de {estornado.Data:dd/MM/yyyy}) estornada. Motivo: {request.Motivo.Trim()}");

            await fichas.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex) { return Falha.Com(request.ValidationResult, ex.Message); }
    }
}

public class DesfazerAprovacaoCommandHandler(
    IInscricaoRepository fichas, ITemporadaRepository temporadas, IEquipeRepository equipes, IAuditoria auditoria)
    : IRequestHandler<DesfazerAprovacaoCommand, ValidationResult>
{
    public async Task<ValidationResult> Handle(DesfazerAprovacaoCommand request, CancellationToken ct)
    {
        if (!request.EhValido()) return request.ValidationResult;
        try
        {
            var ficha = await fichas.ObterPorCategoriaIdAsync(request.InscricaoCategoriaId, ct);
            if (ficha is null) return Falha.Com(request.ValidationResult, "Inscrição não encontrada");
            (await temporadas.ObterPorIdAsync(ficha.TemporadaId, ct))!.ValidarPermiteAlterarInscricoes();

            if (await equipes.AtletaExisteParaAsync(request.InscricaoCategoriaId, ct))
                return Falha.Com(request.ValidationResult, $"{ficha.Pessoa.Nome} está em uma equipe. Retire-o da equipe antes de desfazer a aprovação");

            var pedido = ficha.Categorias.First(c => c.Id == request.InscricaoCategoriaId);
            var estava = pedido.Status;
            ficha.DesfazerAprovacao(request.InscricaoCategoriaId);
            auditoria.Registrar("DesfazerAprovacao", "InscricaoCategoria", pedido.Id,
                $"Pedido de {ficha.Pessoa.Nome} voltou de {estava} para Pendente. Motivo: {request.Motivo.Trim()}");

            await fichas.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex) { return Falha.Com(request.ValidationResult, ex.Message); }
    }
}

public class ReabrirPedidoCommandHandler(IInscricaoRepository fichas, ITemporadaRepository temporadas, IAuditoria auditoria)
    : IRequestHandler<ReabrirPedidoCommand, ValidationResult>
{
    public async Task<ValidationResult> Handle(ReabrirPedidoCommand request, CancellationToken ct)
    {
        if (!request.EhValido()) return request.ValidationResult;
        try
        {
            var ficha = await fichas.ObterPorCategoriaIdAsync(request.InscricaoCategoriaId, ct);
            if (ficha is null) return Falha.Com(request.ValidationResult, "Inscrição não encontrada");
            (await temporadas.ObterPorIdAsync(ficha.TemporadaId, ct))!.ValidarPermiteAlterarInscricoes();

            var pedido = ficha.Categorias.First(c => c.Id == request.InscricaoCategoriaId);
            if (await fichas.ExisteAtivaAsync(ficha.PessoaId, pedido.TemporadaCategoriaId, ct))
                return Falha.Com(request.ValidationResult, $"{ficha.Pessoa.Nome} já tem outro pedido ativo nesta categoria");

            var motivoAntes = pedido.MotivoRecusa;
            ficha.ReabrirPedido(request.InscricaoCategoriaId);
            auditoria.Registrar("ReabrirPedido", "InscricaoCategoria", pedido.Id,
                $"Pedido recusado de {ficha.Pessoa.Nome} reaberto. Motivo da recusa anterior: {motivoAntes}");

            await fichas.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex) { return Falha.Com(request.ValidationResult, ex.Message); }
    }
}

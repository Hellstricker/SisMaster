using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Enums;
using SisMaster.Identidade.Api.Email;
using SisMaster.Identidade.Api.Models;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Controllers;
using SisMaster.WebApi.Core.Usuarios;

namespace SisMaster.Identidade.Api.Controllers;

/// <summary>
/// Gestão de usuários. O Administrador gerencia todos; o Administrador de associação cria e gerencia só Árbitros das
/// associações dele. A senha temporária só existe no e-mail da pessoa: nenhuma resposta da API a devolve.
/// </summary>
[Authorize(Policy = Politicas.Gestao)]
[Route("api/identidade/usuarios")]
public class UsuariosController(
    UserManager<IdentityUser> usuarios,
    IAcessoDoUsuario acesso,
    IEnviadorDeEmail email,
    ILogger<UsuariosController> log) : MainController
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var todos = await usuarios.Users.OrderBy(u => u.Email).ToListAsync();
        var resumos = new List<UsuarioResumo>();
        foreach (var u in todos)
        {
            var r = await Resumir(u);
            if (acesso.EhAdministrador || r.Id == acesso.Id || PodeGerenciar(r)) resumos.Add(r);
        }
        return CustomResponse(resumos);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] NovoUsuario model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);
        if (!ValidarPerfilEAssociacoes(model.Perfil!, model.Associacoes)) return CustomResponse();

        var endereco = model.Email!.Trim();
        if (await usuarios.FindByEmailAsync(endereco) is not null)
        {
            AdicionarErro("Já existe um usuário com este e-mail.");
            return CustomResponse();
        }

        var senha = GerarSenhaTemporaria();
        var usuario = new IdentityUser { UserName = endereco, Email = endereco, EmailConfirmed = true };
        var criado = await usuarios.CreateAsync(usuario, senha);
        if (!criado.Succeeded)
        {
            AdicionarErros(criado.Errors.Select(e => e.Description).ToArray());
            return CustomResponse();
        }
        await usuarios.AddToRoleAsync(usuario, model.Perfil!);
        var claims = new List<Claim> { new(TiposDeClaim.Nome, model.Nome!.Trim()), new(TiposDeClaim.TrocarSenha, "1") };
        claims.AddRange(model.Associacoes.Distinct().Select(a => new Claim(TiposDeClaim.Associacao, a.ToString())));
        await usuarios.AddClaimsAsync(usuario, claims);

        if (!await EnviarSenha(usuario, senha, "Seu acesso ao SisMaster", "Seu acesso foi criado."))
        {
            await usuarios.DeleteAsync(usuario);
            AdicionarErro("Não foi possível enviar o e-mail com a senha temporária. O usuário não foi criado; confira o endereço e tente de novo.");
            return CustomResponse();
        }
        return CustomResponse(await Resumir(usuario));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(string id, [FromBody] EdicaoDeUsuario model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);
        var (usuario, erro) = await Alvo(id);
        if (usuario is null) return erro!;
        var atuais = await usuarios.GetRolesAsync(usuario);
        if (id == acesso.Id && model.Perfil != atuais.FirstOrDefault())
        {
            AdicionarErro("Você não pode mudar o seu próprio perfil.");
            return CustomResponse();
        }
        if (!ValidarPerfilEAssociacoes(model.Perfil!, model.Associacoes)) return CustomResponse();

        await SubstituirClaim(usuario, TiposDeClaim.Nome, model.Nome!.Trim());
        if (!atuais.Contains(model.Perfil!))
        {
            await usuarios.RemoveFromRolesAsync(usuario, atuais);
            await usuarios.AddToRoleAsync(usuario, model.Perfil!);
        }
        var vinculos = (await usuarios.GetClaimsAsync(usuario)).Where(c => c.Type == TiposDeClaim.Associacao).ToList();
        if (vinculos.Count > 0) await usuarios.RemoveClaimsAsync(usuario, vinculos);
        await usuarios.AddClaimsAsync(usuario, model.Associacoes.Distinct().Select(a => new Claim(TiposDeClaim.Associacao, a.ToString())));
        return CustomResponse(await Resumir(usuario));
    }

    [HttpPost("{id}/desativar")]
    public async Task<IActionResult> Desativar(string id)
    {
        if (id == acesso.Id)
        {
            AdicionarErro("Você não pode desativar a sua própria conta.");
            return CustomResponse();
        }
        var (usuario, erro) = await Alvo(id);
        if (usuario is null) return erro!;
        await SubstituirClaim(usuario, TiposDeClaim.Desativado, "1");
        return CustomResponse(await Resumir(usuario));
    }

    [HttpPost("{id}/reativar")]
    public async Task<IActionResult> Reativar(string id)
    {
        var (usuario, erro) = await Alvo(id);
        if (usuario is null) return erro!;
        await RemoverClaim(usuario, TiposDeClaim.Desativado);
        return CustomResponse(await Resumir(usuario));
    }

    /// <summary>Libera uma conta bloqueada por tentativas de senha erradas.</summary>
    [HttpPost("{id}/desbloquear")]
    public async Task<IActionResult> Desbloquear(string id)
    {
        var (usuario, erro) = await Alvo(id);
        if (usuario is null) return erro!;
        await usuarios.SetLockoutEndDateAsync(usuario, null);
        await usuarios.ResetAccessFailedCountAsync(usuario);
        return CustomResponse(await Resumir(usuario));
    }

    /// <summary>Gera outra senha temporária e a envia por e-mail; a pessoa precisa trocá-la no próximo acesso.</summary>
    [HttpPost("{id}/redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(string id)
    {
        var (usuario, erro) = await Alvo(id);
        if (usuario is null) return erro!;

        var senha = GerarSenhaTemporaria();
        var ficha = await usuarios.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await usuarios.ResetPasswordAsync(usuario, ficha, senha);
        if (!resultado.Succeeded)
        {
            AdicionarErros(resultado.Errors.Select(e => e.Description).ToArray());
            return CustomResponse();
        }
        await SubstituirClaim(usuario, TiposDeClaim.TrocarSenha, "1");
        await usuarios.SetLockoutEndDateAsync(usuario, null);
        await usuarios.ResetAccessFailedCountAsync(usuario);

        if (!await EnviarSenha(usuario, senha, "Nova senha temporária do SisMaster", "Uma nova senha temporária foi gerada para você."))
        {
            AdicionarErro("A senha foi redefinida, mas o e-mail não pôde ser enviado. Use Redefinir senha de novo para tentar outra vez.");
            return CustomResponse();
        }
        return CustomResponse(await Resumir(usuario));
    }

    // ---------------------------------------------------------------- regras

    private bool PodeGerenciar(UsuarioResumo alvo) =>
        acesso.EhAdministrador || (alvo.Perfil == Perfis.Arbitro && alvo.Associacoes.Any(acesso.PodeAcessar));

    /// <summary>Localiza o usuário e confere se quem pede pode gerenciá-lo (404 se não existe ou não é dele).</summary>
    private async Task<(IdentityUser? Usuario, IActionResult? Erro)> Alvo(string id)
    {
        var usuario = await usuarios.FindByIdAsync(id);
        if (usuario is null || !PodeGerenciar(await Resumir(usuario))) return (null, NotFound());
        return (usuario, null);
    }

    private bool ValidarPerfilEAssociacoes(string perfil, List<Guid> associacoes)
    {
        if (!Enum.GetNames<PerfilUsuario>().Contains(perfil))
        {
            AdicionarErro("Perfil inválido.");
            return false;
        }
        if (!acesso.EhAdministrador && perfil != Perfis.Arbitro)
        {
            AdicionarErro("Você só pode criar e gerenciar árbitros.");
            return false;
        }
        if (perfil != Perfis.Administrador && associacoes.Count == 0)
        {
            AdicionarErro("Marque ao menos uma associação.");
            return false;
        }
        if (!acesso.EhAdministrador && associacoes.Any(a => !acesso.PodeAcessar(a)))
        {
            AdicionarErro("Você só pode vincular a associações às quais você tem acesso.");
            return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- apoio

    private async Task<UsuarioResumo> Resumir(IdentityUser u)
    {
        var claims = await usuarios.GetClaimsAsync(u);
        var desativado = claims.Any(c => c.Type == TiposDeClaim.Desativado && c.Value == "1");
        var bloqueado = await usuarios.IsLockedOutAsync(u);
        return new UsuarioResumo
        {
            Id = u.Id,
            Email = u.Email ?? "",
            Nome = claims.FirstOrDefault(c => c.Type == TiposDeClaim.Nome)?.Value ?? u.Email ?? "",
            Perfil = (await usuarios.GetRolesAsync(u)).FirstOrDefault(),
            Associacoes = claims.Where(c => c.Type == TiposDeClaim.Associacao)
                .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
            Situacao = desativado ? "Desativado" : bloqueado ? "Bloqueado" : "Ativo",
            SenhaTemporaria = claims.Any(c => c.Type == TiposDeClaim.TrocarSenha && c.Value == "1")
        };
    }

    private async Task SubstituirClaim(IdentityUser u, string tipo, string valor)
    {
        await RemoverClaim(u, tipo);
        await usuarios.AddClaimAsync(u, new Claim(tipo, valor));
    }

    private async Task RemoverClaim(IdentityUser u, string tipo)
    {
        var antigas = (await usuarios.GetClaimsAsync(u)).Where(c => c.Type == tipo).ToList();
        if (antigas.Count > 0) await usuarios.RemoveClaimsAsync(u, antigas);
    }

    private async Task<bool> EnviarSenha(IdentityUser u, string senha, string assunto, string abertura)
    {
        var corpo = $"{abertura}\n\nE-mail de acesso: {u.Email}\nSenha temporária: {senha}\n\n" +
                    "No primeiro acesso o sistema pede para você definir a sua própria senha. Não repasse esta mensagem a ninguém.";
        try
        {
            await email.EnviarAsync(u.Email!, assunto, corpo);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha ao enviar e-mail de senha temporária para {Email}", u.Email);
            return false;
        }
    }

    /// <summary>12 caracteres sem símbolos ambíguos (0/O, 1/l/I), sempre com letras e números.</summary>
    public static string GerarSenhaTemporaria()
    {
        const string letras = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
        const string numeros = "23456789";
        const string todos = letras + numeros;
        var chars = new char[12];
        chars[0] = letras[RandomNumberGenerator.GetInt32(letras.Length)];
        chars[1] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];
        for (var i = 2; i < chars.Length; i++) chars[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

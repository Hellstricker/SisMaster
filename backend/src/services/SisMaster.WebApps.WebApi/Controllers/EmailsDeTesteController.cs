using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SisMaster.WebApps.WebApi.Email;

namespace SisMaster.WebApps.WebApi.Controllers;

/// <summary>
/// Só existe no ambiente de teste (Email:Provedor = Memoria): devolve os e-mails "enviados" a um endereço, para o teste de navegador abrir o
/// link do convite de equipe. Em qualquer outra configuração responde 404, como se a rota não existisse.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/teste")]
public class EmailsDeTesteController(IServiceProvider servicos) : ControllerBase
{
    [HttpGet("emails")]
    public IActionResult Emails([FromQuery] string para)
    {
        var memoria = servicos.GetService<EnviadorDeEmailMemoria>();
        return memoria is null ? NotFound() : Ok(memoria.Mensagens(para ?? string.Empty));
    }
}

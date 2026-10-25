using Esteira.Api.Autenticacao;
using Esteira.Application.Sessao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/auth")]
    public class AuthController(FluxoLogin fluxo, IOptions<AutenticacaoSettings> settings) : ControllerBase
    {
        [HttpGet("login")]
        public async Task<IActionResult> Login([FromQuery] string? returnUrl)
            => Redirect((await fluxo.IniciarAsync(returnUrl)).ToString());

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            var concluido = await fluxo.ConcluirAsync(code, state, error);

            if (concluido is null)
                return LocalRedirect(ReturnUrlSeguro.Raiz);

            Response.Cookies.Append(CookieSessao.Nome, concluido.SessaoId, CookieSessao.Opcoes(settings.Value.RequerHttps));
            return LocalRedirect(concluido.ReturnUrl);
        }
    }
}

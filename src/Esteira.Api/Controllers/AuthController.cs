using Esteira.Api.Autenticacao;
using Esteira.Application.Sessao;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/auth")]
    public class AuthController(FluxoLogin fluxo, FluxoSessao fluxoSessao, IOptions<AutenticacaoSettings> settings) : ControllerBase
    {
        private string? SessaoId => Request.Cookies[CookieSessao.Nome];

        // Anônimo de propósito: o refresh existe justamente para quando o access token já venceu e a sessão
        // autenticada devolveria 401. Quem vale é o cookie, conferido no Redis pelo fluxo.
        [HttpPost("refresh")]
        public async Task<Result> Refresh()
        {
            var resultado = await fluxoSessao.RenovarAsync(SessaoId);

            if (!resultado.IsSuccess && resultado.Errors[0].Type == TipoErro.Unauthorized)
                ExpirarCookie();

            return resultado;
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            var destino = await fluxoSessao.EncerrarAsync(SessaoId);
            ExpirarCookie();
            return Redirect(destino.ToString());
        }

        private void ExpirarCookie()
        {
            var opcoes = CookieSessao.Opcoes(settings.Value.RequerHttps);
            opcoes.Expires = DateTimeOffset.UnixEpoch;
            Response.Cookies.Append(CookieSessao.Nome, string.Empty, opcoes);
        }

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

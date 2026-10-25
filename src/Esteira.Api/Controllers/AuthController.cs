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

        [HttpGet("login")]
        public async Task<IActionResult> Login([FromQuery] string? returnUrl)
        {
            var login = await fluxo.IniciarAsync(returnUrl);
            Response.Cookies.Append(CookieSessao.NomeLogin, login.State, CookieSessao.OpcoesDoLogin(settings.Value.RequerHttps));
            return Redirect(login.Url.ToString());
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            var concluido = await fluxo.ConcluirAsync(code, state, error, Request.Cookies[CookieSessao.NomeLogin]);
            ExpirarCookie(CookieSessao.NomeLogin, CookieSessao.OpcoesDoLogin(settings.Value.RequerHttps));

            if (concluido is null)
                return LocalRedirect(ReturnUrlSeguro.Raiz);

            Response.Cookies.Append(CookieSessao.Nome, concluido.SessaoId, CookieSessao.Opcoes(settings.Value.RequerHttps));
            return LocalRedirect(concluido.ReturnUrl);
        }

        // Anônimo de propósito: o refresh existe justamente para quando o access token já venceu e a sessão
        // autenticada devolveria 401. Quem vale é o cookie, conferido no Redis pelo fluxo.
        [HttpPost("refresh")]
        public async Task<Result> Refresh()
        {
            var resultado = await fluxoSessao.RenovarAsync(SessaoId);

            if (!resultado.IsSuccess && resultado.Errors[0].Type == TipoErro.Unauthorized)
                ExpirarCookie(CookieSessao.Nome, CookieSessao.Opcoes(settings.Value.RequerHttps));

            return resultado;
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            var destino = await fluxoSessao.EncerrarAsync(SessaoId);
            ExpirarCookie(CookieSessao.Nome, CookieSessao.Opcoes(settings.Value.RequerHttps));

            // O redirect leva o id_token_hint na URL: não pode ficar em cache nem ir adiante por Referer.
            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            return Redirect(destino.ToString());
        }

        private void ExpirarCookie(string nome, CookieOptions opcoes)
        {
            opcoes.Expires = DateTimeOffset.UnixEpoch;
            opcoes.MaxAge = null;
            Response.Cookies.Append(nome, string.Empty, opcoes);
        }
    }
}

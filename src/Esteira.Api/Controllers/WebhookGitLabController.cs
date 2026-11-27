using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Esteira.Api.Autenticacao;
using Esteira.Api.Erros;
using Esteira.Application.Eventos;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Esteira.Api.Controllers
{
    // Terceira superfície (T1.3, ADR-0011): o GitLab chama sem sessão, autenticado pelo segredo compartilhado
    // `GitLab:Webhook:Secret`. Ordem fixa: cabeçalhos -> token (tempo constante) -> só então o corpo. Nada do corpo nem
    // dos cabeçalhos vai para log ou resposta.
    [ApiController]
    [Route("api/webhooks")]
    [AllowAnonymous]
    public class WebhookGitLabController(ITratadorDeWebhookGitLab tratador, IConfiguration configuracao) : ControllerBase
    {
        public const int TamanhoMaximoDoCorpo = 1024 * 1024;
        public const string AtorDoWebhook = "webhook-gitlab";

        private static readonly Erro TokenInvalido = new(
            "webhook.token-invalido", "O token do webhook está ausente ou é inválido.", TipoErro.Unauthorized);

        [HttpPost("gitlab")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<RespostaDeErro>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        public async Task<IActionResult> Receber(CancellationToken cancellationToken)
        {
            var token = Request.Headers["X-Gitlab-Token"].ToString();
            var evento = Request.Headers["X-Gitlab-Event"].ToString();

            if (!TokenConfere(token, configuracao["GitLab:Webhook:Secret"]))
                return new ObjectResult(Result.Falha(TokenInvalido));

            // A rota é anônima, mas a conclusão da Ação audita "quem": o ator é o próprio webhook (nunca uma pessoa nem o token).
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(UsuarioAtual.ClaimId, AtorDoWebhook)], authenticationType: "WebhookGitLab"));

            // Defesa em profundidade: o servidor também recusa no limite; a leitura abaixo vale em qualquer host.
            var limite = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limite is { IsReadOnly: false })
                limite.MaxRequestBodySize = TamanhoMaximoDoCorpo;

            if (Request.ContentLength > TamanhoMaximoDoCorpo)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);

            var corpo = await LerCorpoAsync(cancellationToken);
            if (corpo is null)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);

            var resultado = await tratador.TratarAsync(evento, corpo, cancellationToken);
            return resultado.IsSuccess ? Ok() : new ObjectResult(resultado);
        }

        // Compara os SHA-256 dos dois lados em tempo constante: o tamanho do segredo também não vaza.
        private static bool TokenConfere(string recebido, string? segredo)
        {
            if (string.IsNullOrEmpty(segredo) || string.IsNullOrEmpty(recebido))
                return false;

            var esperado = SHA256.HashData(Encoding.UTF8.GetBytes(segredo));
            var enviado = SHA256.HashData(Encoding.UTF8.GetBytes(recebido));
            return CryptographicOperations.FixedTimeEquals(esperado, enviado);
        }

        // null = passou de 1 MB (mesmo sem Content-Length, em chunked).
        private async Task<string?> LerCorpoAsync(CancellationToken ct)
        {
            using var memoria = new MemoryStream();
            var bloco = new byte[16 * 1024];
            int lidos;
            try
            {
                while ((lidos = await Request.Body.ReadAsync(bloco, ct)) > 0)
                {
                    if (memoria.Length + lidos > TamanhoMaximoDoCorpo)
                        return null;
                    memoria.Write(bloco, 0, lidos);
                }
            }
            catch (BadHttpRequestException)
            {
                return null;
            }

            return Encoding.UTF8.GetString(memoria.GetBuffer(), 0, (int)memoria.Length);
        }
    }
}

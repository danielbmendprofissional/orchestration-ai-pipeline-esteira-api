using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Esteira.Integration.Tests.Pipeline
{
    // GitLab falso (E1.3): serve o discovery e o JWKS de uma chave RSA própria e assina id_tokens com ela.
    // A API valida de verdade (assinatura, emissor, audiência, validade) contra este emissor.
    internal sealed class EmissorJwksFalso
    {
        public const string Url = "https://gitlab.fake";

        private readonly RSA _rsa = RSA.Create(2048);
        private const string Kid = "chave-de-teste";

        public HttpMessageHandler Handler => new ServidorDoEmissor(this);

        public string Jwks()
        {
            var p = _rsa.ExportParameters(false);
            return $$"""{"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{Kid}}","n":"{{Base64UrlEncoder.Encode(p.Modulus)}}","e":"{{Base64UrlEncoder.Encode(p.Exponent)}}"}]}""";
        }

        public string IdToken(
            string projectPath,
            string emissor = Url,
            string audiencia = "esteira",
            TimeSpan? expiraEm = null,
            RSA? chave = null,
            IDictionary<string, object>? extras = null)
        {
            var agora = DateTime.UtcNow;
            var expira = expiraEm ?? TimeSpan.FromMinutes(5);
            var claims = new Dictionary<string, object> { ["project_path"] = projectPath, ["sub"] = "project_path:" + projectPath };
            if (extras is not null)
                foreach (var (k, v) in extras)
                    claims[k] = v;

            var descritor = new SecurityTokenDescriptor
            {
                Issuer = emissor,
                Audience = audiencia,
                IssuedAt = agora.AddMinutes(-30),
                NotBefore = agora.AddMinutes(-30),
                Expires = agora + expira,
                Claims = claims,
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(chave ?? _rsa) { KeyId = Kid }, SecurityAlgorithms.RsaSha256)
            };
            return new JsonWebTokenHandler().CreateToken(descritor);
        }

        // Chave que o emissor NÃO publica: token assinado com ela tem a assinatura inválida.
        public static RSA ChaveEstranha() => RSA.Create(2048);

        private sealed class ServidorDoEmissor(EmissorJwksFalso emissor) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var caminho = request.RequestUri!.AbsolutePath;
                var corpo = caminho switch
                {
                    "/.well-known/openid-configuration" => $$"""{"issuer":"{{Url}}","jwks_uri":"{{Url}}/oauth/discovery/keys"}""",
                    "/oauth/discovery/keys" => emissor.Jwks(),
                    _ => null
                };

                var resposta = corpo is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
                return Task.FromResult(resposta);
            }
        }
    }
}

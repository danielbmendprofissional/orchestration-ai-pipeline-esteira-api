using Esteira.Api.Erros;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Esteira.Api.Pipeline
{
    // Autenticação da superfície /api/pipeline/v2 (ADR-0008, ADR-0013): `id_token` do GitLab no Authorization: Bearer.
    // JWKS descoberto em `GitLab:Url`; audiência `Pipeline:Audience` (padrão `esteira`); emissor = `GitLab:Url`.
    // Esquema próprio: não mistura com a sessão BFF do front (ADR-0004) e não toca o banco nem o Redis.
    public static class PipelineAutenticacao
    {
        public const string Esquema = "PipelineIdToken";
        public const string AudienciaPadrao = "esteira";
        public const string ClaimProjeto = "project_path";

        public static readonly Erro TokenInvalido = new(
            "pipeline.token-invalido", "O id_token da pipeline é ausente ou inválido.", TipoErro.Unauthorized);

        public static AuthenticationBuilder AddPipelineIdToken(this AuthenticationBuilder auth, IConfiguration configuration)
            => auth.AddJwtBearer(Esquema, opcoes =>
            {
                // Lido na hora de montar as opções (não no boot): sem `GitLab:Url` a rota responde 401, não derruba a API.
                var gitlab = configuration["GitLab:Url"]?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(gitlab))
                    opcoes.MetadataAddress = "https://gitlab-nao-configurado.invalid/.well-known/openid-configuration";
                else
                    opcoes.Authority = gitlab;

                opcoes.MapInboundClaims = false;
                opcoes.SaveToken = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = gitlab,
                    ValidateAudience = true,
                    ValidAudience = string.IsNullOrWhiteSpace(configuration["Pipeline:Audience"])
                        ? AudienciaPadrao
                        : configuration["Pipeline:Audience"],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Qualquer falha (cabeçalho ausente/malformado, assinatura, emissor, audiência, validade) é o mesmo 401,
                // sem eco do token nem do motivo.
                opcoes.Events = new JwtBearerEvents
                {
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await contexto.Response.WriteAsJsonAsync(RespostaDeErro.De([TokenInvalido]));
                    }
                };
            });
    }
}

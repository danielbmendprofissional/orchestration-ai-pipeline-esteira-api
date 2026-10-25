using Esteira.Application.Interfaces.Sessao;
using Esteira.Application.Sessao;
using Esteira.Domain.Common.Resultados;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Esteira.Api.Autenticacao
{
    // Refresh e logout da sessão BFF (ADR-0004).
    public sealed class FluxoSessao(
        ISessaoStore sessoes,
        IProvedorOidc provedor,
        IOptions<AutenticacaoSettings> settings,
        IConfiguration configuracao,
        ILogger<FluxoSessao> log)
    {
        private static readonly TimeSpan EsperaMaximaPelaTrava = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan IntervaloDaTrava = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan DuracaoPadraoDaSessao = TimeSpan.FromMinutes(30);

        private static readonly Erro NaoAutenticado = new(
            SessaoAuthenticationHandler.CodeNaoAutenticado, "Entre para continuar.", TipoErro.Unauthorized);

        // Rotação do refresh token. O refresh token do Keycloak é de uso único: duas trocas simultâneas com o
        // mesmo token fariam a segunda parecer reuso e derrubar o usuário. Por isso a troca é serializada por
        // sessão e quem esperou a trava só confere se outro chamador já renovou.
        public async Task<Result> RenovarAsync(string? sessaoId)
        {
            if (!IdentificadorSeguro.Valido(sessaoId))
                return Result.Falha(NaoAutenticado);

            var inicial = await sessoes.ObterAsync(sessaoId);

            if (inicial is null)
                return Result.Falha(NaoAutenticado);

            var dono = await AguardarTravaAsync(sessaoId);

            if (dono is null)
            {
                log.LogWarning("Trava de refresh não liberou a tempo para a sessão");
                return Result.Falha(new Erro("auth.provedor-indisponivel", "Tente de novo em instantes.", TipoErro.Failure));
            }

            try
            {
                var atual = await sessoes.ObterAsync(sessaoId);

                if (atual is null)
                    return Result.Falha(NaoAutenticado);

                // Outro chamador renovou enquanto esperávamos a trava: os tokens novos já estão na sessão.
                if (atual.RefreshToken != inicial.RefreshToken)
                    return Result.Sucesso();

                var troca = await provedor.RenovarAsync(atual.RefreshToken);

                if (!troca.IsSuccess)
                    return await TratarFalhaAsync(atual, troca.Errors[0]);

                var tokens = troca.Value;
                var duracao = tokens.RefreshExpiresIn > 0 ? TimeSpan.FromSeconds(tokens.RefreshExpiresIn) : DuracaoPadraoDaSessao;
                var atualizou = await sessoes.AtualizarAsync(
                    atual with
                    {
                        AccessToken = tokens.AccessToken,
                        RefreshToken = tokens.RefreshToken,
                        // O Keycloak pode não reenviar o id_token; ele ainda é a dica do logout.
                        IdToken = string.IsNullOrEmpty(tokens.IdToken) ? atual.IdToken : tokens.IdToken,
                        ExpiraEm = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
                    },
                    duracao);

                // Logout ou revogação geral durante a troca: a sessão não volta com os tokens novos.
                return atualizou ? Result.Sucesso() : Result.Falha(NaoAutenticado);
            }
            finally
            {
                await sessoes.LiberarRefreshAsync(sessaoId, dono);
            }
        }

        // Encerra a sessão local e devolve para onde mandar o navegador: o end-session do Keycloak (que encerra
        // o SSO e volta para a raiz) ou direto a raiz quando não há sessão para encerrar.
        public async Task<Uri> EncerrarAsync(string? sessaoId)
        {
            var raiz = new Uri(ReturnUrlSeguro.Raiz, UriKind.Relative);

            if (!IdentificadorSeguro.Valido(sessaoId))
                return raiz;

            var sessao = await sessoes.ObterAsync(sessaoId);
            await sessoes.RevogarAsync(sessaoId);

            if (sessao is null)
                return raiz;

            var endpoints = await provedor.ObterEndpointsAsync();

            if (endpoints.EndSession is null)
                return raiz;

            var parametros = new Dictionary<string, string?>
            {
                ["id_token_hint"] = sessao.IdToken,
                ["post_logout_redirect_uri"] = $"{settings.Value.UrlPublica.TrimEnd('/')}/",
                ["client_id"] = configuracao["Keycloak:ClientId"]
            };

            return new Uri(QueryHelpers.AddQueryString(endpoints.EndSession.ToString(), parametros));
        }

        private async Task<string?> AguardarTravaAsync(string sessaoId)
        {
            var limite = DateTimeOffset.UtcNow + EsperaMaximaPelaTrava;

            while (true)
            {
                var dono = await sessoes.TentarTravarRefreshAsync(sessaoId);

                if (dono is not null)
                    return dono;

                if (DateTimeOffset.UtcNow >= limite)
                    return null;

                await Task.Delay(IntervaloDaTrava);
            }
        }

        private async Task<Result> TratarFalhaAsync(SessaoUsuario sessao, Erro erro)
        {
            if (erro.Code == "auth.sessao-invalida")
            {
                // Reuse detection: refresh token já usado ou revogado = possível roubo; derruba todas as sessões da pessoa.
                log.LogWarning("Refresh recusado (invalid_grant): revogando todas as sessões do usuário");
                await sessoes.RevogarTodasDoUsuarioAsync(sessao.Sub);
            }

            return Result.Falha(erro);
        }
    }
}

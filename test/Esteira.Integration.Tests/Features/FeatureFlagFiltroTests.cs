using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Api.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Integration.Tests.Features
{
    // Critério 17 da A1: flag ausente ou diferente de true = desligado (404), nunca ligado por omissão.
    [Trait("Fatia", "A1")]
    public class FeatureFlagFiltroTests
    {
        private static ResourceExecutingContext Contexto(Dictionary<string, string?> config)
        {
            var configuracao = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
            var http = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddSingleton<IConfiguration>(configuracao).BuildServiceProvider()
            };
            return new ResourceExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], []);
        }

        [Theory, DisplayName("Flag ausente, vazio ou diferente de true responde 404 sem corpo")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("false")]
        [InlineData("0")]
        [InlineData("talvez")]
        public void Desligado_Responde404(string? valor)
        {
            var config = valor is null ? new Dictionary<string, string?>() : new Dictionary<string, string?> { ["Features:A1"] = valor };
            var contexto = Contexto(config);

            new FeatureFlagAttribute("A1").OnResourceExecuting(contexto);

            contexto.Result.Should().BeOfType<NotFoundResult>();
        }

        [Theory, DisplayName("Flag true (qualquer caixa) deixa a requisição seguir")]
        [InlineData("true")]
        [InlineData("True")]
        public void Ligado_Segue(string valor)
        {
            var contexto = Contexto(new Dictionary<string, string?> { ["Features:A1"] = valor });

            new FeatureFlagAttribute("A1").OnResourceExecuting(contexto);

            contexto.Result.Should().BeNull();
        }

        [Fact, DisplayName("Lê a chave da própria Fatia, sem confundir com outra")]
        public void OutraFatia_NaoLiga()
        {
            var contexto = Contexto(new Dictionary<string, string?> { ["Features:B2"] = "true" });

            new FeatureFlagAttribute("A1").OnResourceExecuting(contexto);

            contexto.Result.Should().BeOfType<NotFoundResult>();
        }
    }
}

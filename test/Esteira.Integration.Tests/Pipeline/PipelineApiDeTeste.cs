using Esteira.Application.Entregas.Pipeline;
using Esteira.Integration.Tests.Http;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;

namespace Esteira.Integration.Tests.Pipeline
{
    // A API de verdade (Program) com o GitLab falso como emissor do id_token (E1.3) e PostgreSQL real.
    public sealed class PipelineApiDeTeste(InfraestruturaFixture infra) : WebApplicationFactory<Program>
    {
        internal EmissorJwksFalso Emissor { get; } = new();

        // Conta as idas ao banco da autorização do Projeto e os repasses ao ponto de extensão da E1.4.
        internal ConsultaEspia Consultas { get; } = new();

        internal ExecucaoEspia Execucoes { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Keycloak:Authority", "https://keycloak.fake/realms/esteira");
            builder.UseSetting("Keycloak:ClientId", "esteira-api");
            builder.UseSetting("Keycloak:ClientSecret", "segredo-de-teste");
            builder.UseSetting("Autenticacao:RequerHttps", "false");
            builder.UseSetting("GitLab:Url", EmissorJwksFalso.Url);

            foreach (var (chave, valor) in infra.Configuracao("t" + Guid.NewGuid().ToString("N")[..11]))
                builder.UseSetting(chave, valor);

            builder.ConfigureTestServices(services =>
            {
                services.Configure<JwtBearerOptions>(PipelineEsquema, o => o.BackchannelHttpHandler = Emissor.Handler);

                services.RemoveAll<IProjetoDaPipelineQuery>();
                services.AddScoped<IProjetoDaPipelineQuery>(sp =>
                    new ConsultaEspiaDoProjeto(Consultas, ActivatorUtilities.CreateInstance<Esteira.Data.Entregas.ProjetoDaPipelineQuery>(sp)));

                services.RemoveAll<IAbrirPipeline>();
                services.AddSingleton<IAbrirPipeline>(Execucoes);

                services.AddSingleton<LogDeTeste>();
                services.AddSingleton<ILogEventSink>(sp => sp.GetRequiredService<LogDeTeste>());
            });
        }

        public const string PipelineEsquema = "PipelineIdToken";
    }

    internal sealed class ConsultaEspia
    {
        private int _chamadas;
        public int Chamadas => _chamadas;
        public void Registrar() => Interlocked.Increment(ref _chamadas);
        public void Zerar() => Interlocked.Exchange(ref _chamadas, 0);
    }

    internal sealed class ConsultaEspiaDoProjeto(ConsultaEspia espia, IProjetoDaPipelineQuery real) : IProjetoDaPipelineQuery
    {
        public Task<bool> ProjetoAtivoCadastradoAsync(string caminho, CancellationToken cancellationToken = default)
        {
            espia.Registrar();
            return real.ProjetoAtivoCadastradoAsync(caminho, cancellationToken);
        }
    }

    // Ponto de extensão da E1.4: o teste devolve o que ele quiser e vê o que a rota repassou.
    internal sealed class ExecucaoEspia : IAbrirPipeline
    {
        public List<AbrirPipelineEntrada> Recebidas { get; } = [];

        public Esteira.Domain.Common.Resultados.Result<AbrirPipelineResposta> Resposta { get; set; } =
            Esteira.Domain.Common.Resultados.Result<AbrirPipelineResposta>.Sucesso(
                new AbrirPipelineResposta("B2=true", [new DestinoNaResposta("local", 1, new Dictionary<string, string> { ["K8S_NAMESPACE"] = "qa" })], 2.00m));

        public Task<Esteira.Domain.Common.Resultados.Result<AbrirPipelineResposta>> ExecutarAsync(
            AbrirPipelineEntrada entrada, CancellationToken cancellationToken = default)
        {
            lock (Recebidas)
                Recebidas.Add(entrada);
            return Task.FromResult(Resposta);
        }
    }
}

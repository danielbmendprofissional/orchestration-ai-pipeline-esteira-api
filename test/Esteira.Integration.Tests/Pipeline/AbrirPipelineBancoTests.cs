using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Entregas;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Entregas;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Pipeline
{
    // E1.4, critérios 15 e 8/12: leitura dos Destinos da A1 e corrida de duas `promocao`, com PostgreSQL real.
    [Trait("Fatia", "E1")]
    public sealed class AbrirPipelineBancoTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<Result<AbrirPipelineResposta>> ExecutarAsync(CenarioDePipeline c, FluxoEntity fluxo, string process, string env)
        {
            await using var escrita = ContextoDoCadastro.Novo(infra);
            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var handler = new AbrirPipelineHandler(
                new ProjetoPorCaminhoQuery(leitura), new EntregaRepositorio(escrita), new ProjetoRepositorio(escrita),
                new OcupacaoAmbienteRepositorio(escrita), new DestinosDaPipelineQuery(leitura), new UnitOfWork(escrita), TimeProvider.System);
            var entrega = await escrita.Set<EntregaEntity>().AsNoTracking().FirstAsync(e => e.Id == fluxo.EntregaId, Ct);
            return await handler.ExecutarAsync(
                new AbrirPipelineEntrada(process, env, entrega.PublicId, fluxo.PublicId, CenarioDePipeline.Sha, c.Caminho, "1", null, null, null, false), Ct);
        }

        [Fact, DisplayName("15: Destinos do Ambiente vêm por Ordem com a configuração cadastrada; outro Ambiente não vaza")]
        public async Task DestinosPorOrdemComConfiguracao()
        {
            var c = await CenarioDePipeline.CriarAsync(infra, true,
                (AmbienteDeploy.Qa, "segundo", 2, new() { ["K8S_NAMESPACE"] = "qa", ["APP_URL"] = "https://qa" }),
                (AmbienteDeploy.Qa, "primeiro", 1, new()),
                (AmbienteDeploy.Hml, "so-hml", 1, new() { ["X"] = "y" }));

            var r = await ExecutarAsync(c, c.Fluxo, "fluxo", "qa");

            r.IsSuccess.Should().BeTrue();
            r.Value.FeatureFlags.Should().Be("B2=true");
            r.Value.Destinos.Select(d => (d.Nome, d.Ordem)).Should().Equal(("primeiro", 1), ("segundo", 2));
            r.Value.Destinos[1].Configuracao.Should().BeEquivalentTo(new Dictionary<string, string> { ["K8S_NAMESPACE"] = "qa", ["APP_URL"] = "https://qa" });
            r.Value.Destinos[0].Configuracao.Should().BeEmpty();
        }

        [Fact, DisplayName("15: Ambiente habilitado sem Destino devolve lista vazia; Ambiente desabilitado é recusado")]
        public async Task SemDestinoVazio_DesabilitadoRecusa()
        {
            var c = await CenarioDePipeline.CriarAsync(infra, hmlHabilitado: false);

            (await ExecutarAsync(c, c.Fluxo, "fluxo", "qa")).Value.Destinos.Should().BeEmpty();

            var r = await ExecutarAsync(c, c.Fluxo, "promocao", "hml");
            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Code.Should().Be("ambiente.desabilitado");
        }

        [Fact, DisplayName("8/12: duas promocao simultâneas de Fluxos do mesmo Projeto em hml: uma 200, a outra 409 ambiente.ocupado, nunca exceção")]
        public async Task CorridaDePromocao()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            var outro = await c.OutroFluxoAsync(infra);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                foreach (var id in new[] { c.Fluxo.Id, outro.Id })
                {
                    var f = await ctx.Set<FluxoEntity>().FirstAsync(x => x.Id == id, Ct);
                    f.RegistrarSha(CenarioDePipeline.Sha);
                }
                await ctx.SaveChangesAsync(Ct);
            }

            var resultados = await Task.WhenAll(
                Task.Run(() => ExecutarAsync(c, c.Fluxo, "promocao", "hml"), Ct),
                Task.Run(() => ExecutarAsync(c, outro, "promocao", "hml"), Ct));

            resultados.Count(r => r.IsSuccess).Should().Be(1);
            resultados.Single(r => !r.IsSuccess).Errors.Should().ContainSingle()
                .Which.Should().Match<Erro>(e => e.Code == "ambiente.ocupado" && e.Type == TipoErro.Conflict);

            await using var leitura = ContextoDoCadastro.Novo(infra);
            (await leitura.Set<OcupacaoAmbienteEntity>().CountAsync(o => o.ProjetoId == c.Projeto.Id, Ct)).Should().Be(1);
        }
    }
}

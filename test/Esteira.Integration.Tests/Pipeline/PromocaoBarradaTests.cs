using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Entregas.Pipeline;
using Esteira.Data.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Entregas;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Pipeline
{
    // E2.3, criterio 9: Etapa anterior bloqueada barra a promocao; o Atalho (fluxo em production) nao e barrado por isso.
    [Trait("Fatia", "E2")]
    public sealed class PromocaoBarradaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private async Task<CenarioDePipeline> CenarioComQaBloqueadaAsync()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            var fluxo = (await new EntregaRepositorio(ctx).ObterFluxoPorPublicIdAsync(c.Fluxo.PublicId, Ct))!;
            fluxo.RegistrarSha(CenarioDePipeline.Sha).IsSuccess.Should().BeTrue();
            fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(1, DateTime.UtcNow).IsSuccess.Should().BeTrue();
            fluxo.Etapa(EtapaDoFluxo.Qa).Bloquear("resultado.bloqueado", DateTime.UtcNow).IsSuccess.Should().BeTrue();
            await ctx.SaveChangesAsync(Ct);
            return c;
        }

        private async Task<Result<AbrirPipelineResposta>> AbrirAsync(CenarioDePipeline c, string process, string env)
        {
            await using var escrita = ContextoDoCadastro.Novo(infra);
            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var handler = new AbrirPipelineHandler(
                new ProjetoPorCaminhoQuery(leitura), new EntregaRepositorio(escrita), new ProjetoRepositorio(escrita),
                new OcupacaoAmbienteRepositorio(escrita), new DestinosDaPipelineQuery(leitura), new UnitOfWork(escrita), TimeProvider.System,
                new TetoCustoDaAplicacaoQuery(leitura), new OpcoesAgentesFixas());
            return await handler.ExecutarAsync(
                new AbrirPipelineEntrada(process, env, c.Entrega.PublicId, c.Fluxo.PublicId, CenarioDePipeline.Sha, c.Caminho, "2", null, "fulano", "motivo", false), Ct);
        }

        [Theory, DisplayName("9: promocao para hml ou production com a Etapa qa bloqueada responde 409 pipeline.etapa-anterior-bloqueada e nao reserva o Ambiente")]
        [InlineData("hml")]
        [InlineData("production")]
        public async Task Promocao_Barrada(string env)
        {
            var c = await CenarioComQaBloqueadaAsync();

            var r = await AbrirAsync(c, "promocao", env);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "pipeline.etapa-anterior-bloqueada" && e.Type == TipoErro.Conflict);
            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await ctx.Set<OcupacaoAmbienteEntity>().CountAsync(o => o.FluxoId == c.Fluxo.Id, Ct)).Should().Be(0);
        }

        [Fact, DisplayName("9: o Atalho (fluxo em production) nao e barrado pela Etapa anterior bloqueada")]
        public async Task Atalho_NaoBarrado()
        {
            var c = await CenarioComQaBloqueadaAsync();

            var r = await AbrirAsync(c, "fluxo", "production");

            r.IsSuccess.Should().BeTrue(string.Join(",", r.Errors.Select(e => e.Code)));
        }
    }
}

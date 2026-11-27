using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Data.Entregas;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, critérios 11, 12 e 13): ocupação de Projeto×Ambiente no PostgreSQL real.
    [Trait("Fatia", "E1")]
    public class OcupacaoAmbienteRepositorioTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = CenarioDeEntrega.T0;

        private async Task<int> LinhasAsync(long projetoId)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await ctx.Set<OcupacaoAmbienteEntity>().CountAsync(o => o.ProjetoId == projetoId, Ct);
        }

        private async Task<Result> ReservarAsync(long projetoId, AmbienteDeploy ambiente, long fluxoId)
        {
            await using var ctx = ContextoDoCadastro.Novo(infra);
            return await new OcupacaoAmbienteRepositorio(ctx).ReservarAsync(projetoId, ambiente, fluxoId, T0, Ct);
        }

        [Theory, DisplayName("Critério 11: Ambiente livre é reservado para o Fluxo; repetido pelo mesmo Fluxo, sem segunda reserva")]
        [InlineData(AmbienteDeploy.Hml)]
        [InlineData(AmbienteDeploy.Producao)]
        public async Task ReservaEIdempotencia(AmbienteDeploy ambiente)
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var projeto = c.Projetos[0].Id;
            var fluxo = c.Fluxo().Id;

            (await ReservarAsync(projeto, ambiente, fluxo)).IsSuccess.Should().BeTrue();
            (await ReservarAsync(projeto, ambiente, fluxo)).IsSuccess.Should().BeTrue();

            await using var ctx = ContextoDoCadastro.Novo(infra);
            var o = (await new OcupacaoAmbienteRepositorio(ctx).ObterAsync(projeto, ambiente, Ct))!;
            (o.FluxoId, o.OcupadaEm).Should().Be((fluxo, T0));
            (await LinhasAsync(projeto)).Should().Be(1);

            var bruto = await ctx.Database.SqlQuery<string>(
                $"SELECT \"Ambiente\" AS \"Value\" FROM esteira.\"OcupacaoAmbiente\" WHERE \"ProjetoId\" = {projeto}").SingleAsync(Ct);
            bruto.Should().Be(ambiente == AmbienteDeploy.Hml ? "hml" : "producao");
        }

        [Fact, DisplayName("Critério 12: Ambiente reservado por outro Fluxo -> ambiente.ocupado, e a reserva original fica")]
        public async Task OcupadoPorOutro()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var projeto = c.Projetos[0].Id;
            var outra = await CenarioDeEntrega.CriarAsync(infra);
            await ReservarAsync(projeto, AmbienteDeploy.Hml, c.Fluxo().Id);

            var r = await ReservarAsync(projeto, AmbienteDeploy.Hml, outra.Fluxo().Id);

            r.IsSuccess.Should().BeFalse();
            r.Errors.Single().Code.Should().Be("ambiente.ocupado");
            await using var ctx = ContextoDoCadastro.Novo(infra);
            (await new OcupacaoAmbienteRepositorio(ctx).ObterAsync(projeto, AmbienteDeploy.Hml, Ct))!.FluxoId.Should().Be(c.Fluxo().Id);
        }

        [Fact, DisplayName("Critério 12: hml e produção do mesmo Projeto são reservas independentes")]
        public async Task AmbientesIndependentes()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var outra = await CenarioDeEntrega.CriarAsync(infra);
            var projeto = c.Projetos[0].Id;

            (await ReservarAsync(projeto, AmbienteDeploy.Hml, c.Fluxo().Id)).IsSuccess.Should().BeTrue();
            (await ReservarAsync(projeto, AmbienteDeploy.Producao, outra.Fluxo().Id)).IsSuccess.Should().BeTrue();
        }

        [Fact, DisplayName("Critério 12: duas reservas simultâneas por Fluxos diferentes -> exatamente uma, a outra ambiente.ocupado, nunca exceção")]
        public async Task Corrida()
        {
            for (var rodada = 0; rodada < 10; rodada++)
            {
                var a = await CenarioDeEntrega.CriarAsync(infra);
                var b = await CenarioDeEntrega.CriarAsync(infra);
                var projeto = a.Projetos[0].Id;
                var largada = new TaskCompletionSource();

                async Task<Result> Tenta(long fluxo)
                {
                    await largada.Task;
                    return await ReservarAsync(projeto, AmbienteDeploy.Producao, fluxo);
                }

                var t1 = Task.Run(() => Tenta(a.Fluxo().Id), Ct);
                var t2 = Task.Run(() => Tenta(b.Fluxo().Id), Ct);
                largada.SetResult();
                var resultados = await Task.WhenAll(t1, t2);

                resultados.Count(r => r.IsSuccess).Should().Be(1, $"rodada {rodada}");
                resultados.Single(r => !r.IsSuccess).Errors.Single().Code.Should().Be("ambiente.ocupado");
                (await LinhasAsync(projeto)).Should().Be(1);
            }
        }

        [Fact, DisplayName("Critério 12: o banco recusa segunda linha de Projeto×Ambiente (índice único)")]
        public async Task IndiceUnico()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var outra = await CenarioDeEntrega.CriarAsync(infra);
            await ReservarAsync(c.Projetos[0].Id, AmbienteDeploy.Hml, c.Fluxo().Id);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"OcupacaoAmbiente\" (\"PublicId\", \"ProjetoId\", \"Ambiente\", \"FluxoId\", \"OcupadaEm\") VALUES ({Guid.CreateVersion7()}, {c.Projetos[0].Id}, 'hml', {outra.Fluxo().Id}, now())", Ct);

            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Critério 13: qa não tem ocupação; dois Fluxos do mesmo Projeto reservam qa sem se bloquear")]
        public async Task QaSemFila()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var outra = await CenarioDeEntrega.CriarAsync(infra);
            var projeto = c.Projetos[0].Id;

            (await ReservarAsync(projeto, AmbienteDeploy.Qa, c.Fluxo().Id)).IsSuccess.Should().BeTrue();
            (await ReservarAsync(projeto, AmbienteDeploy.Qa, outra.Fluxo().Id)).IsSuccess.Should().BeTrue();

            (await LinhasAsync(projeto)).Should().Be(0);
        }

        [Fact, DisplayName("O banco recusa Ambiente fora de hml e produção na ocupação")]
        public async Task SoHmlEProducao()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using var ctx = ContextoDoCadastro.Novo(infra);

            var act = () => ctx.Database.ExecuteSqlAsync(
                $"INSERT INTO esteira.\"OcupacaoAmbiente\" (\"PublicId\", \"ProjetoId\", \"Ambiente\", \"FluxoId\", \"OcupadaEm\") VALUES ({Guid.CreateVersion7()}, {c.Projetos[0].Id}, 'qa', {c.Fluxo().Id}, now())", Ct);

            (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        [Fact, DisplayName("Liberar: o Fluxo dono libera (e de novo é idempotente); depois outro Fluxo reserva")]
        public async Task Libera()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var outra = await CenarioDeEntrega.CriarAsync(infra);
            var projeto = c.Projetos[0].Id;
            await ReservarAsync(projeto, AmbienteDeploy.Hml, c.Fluxo().Id);

            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var repo = new OcupacaoAmbienteRepositorio(ctx);
                (await repo.LiberarAsync(projeto, AmbienteDeploy.Hml, outra.Fluxo().Id, Ct)).Errors.Single().Code.Should().Be("ambiente.ocupado");
                (await repo.LiberarAsync(projeto, AmbienteDeploy.Hml, c.Fluxo().Id, Ct)).IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync(Ct);
                (await repo.LiberarAsync(projeto, AmbienteDeploy.Hml, c.Fluxo().Id, Ct)).IsSuccess.Should().BeTrue();
            }

            (await LinhasAsync(projeto)).Should().Be(0);
            (await ReservarAsync(projeto, AmbienteDeploy.Hml, outra.Fluxo().Id)).IsSuccess.Should().BeTrue();
        }
    }
}

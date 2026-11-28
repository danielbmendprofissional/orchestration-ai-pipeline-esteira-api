using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Entregas;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esteira.Integration.Tests.Analises
{
    // E2 (spec 03-analises-e-gate.md, "Dados"): PostgreSQL real, schema pelas migrations.
    [Trait("Fatia", "E2")]
    public class AnalisePersistenciaTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;
        private static readonly DateTime T0 = new(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

        private static AnaliseEntity Nova(long fluxoId, string? destino = null, long pipeline = 1)
            => AnaliseEntity.Criar(fluxoId, AmbienteDeploy.Qa, destino is null ? EscopoAnalise.Artefato : EscopoAnalise.Deploy, destino, "opengrep",
                TipoAnalise.Ferramenta, VereditoAnalise.Reprovado, null, pipeline, T0,
                [new NovoAchado(SeveridadeAchado.Alta, "SQL injection", "csharp.sqli", "a.cs", 3, "d")]).Value;

        [Fact, DisplayName("Análise e Achados persistem; ChaveDoTipo e custo voltam do banco")]
        public async Task Persiste()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            long id;
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var a = Nova(c.Fluxo().Id);
                ctx.Add(a);
                await ctx.SaveChangesAsync(Ct);
                id = a.Id;
            }

            await using var leitura = ContextoDoCadastro.Novo(infra);
            var lida = await leitura.Set<AnaliseEntity>().Include(a => a.Achados).SingleAsync(a => a.Id == id, Ct);
            lida.Achados.Should().ContainSingle().Which.ChaveDoTipo.Should().Be("opengrep:csharp.sqli");
        }

        [Fact, DisplayName("Índice único: o mesmo job (inclusive destino nulo) não duplica a Análise")]
        public async Task IndiceUnico()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(Nova(c.Fluxo().Id));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var outro = ContextoDoCadastro.Novo(infra);
            outro.Add(Nova(c.Fluxo().Id));
            var act = () => outro.SaveChangesAsync(Ct);

            var ex = await act.Should().ThrowAsync<DbUpdateException>();
            ex.Which.InnerException.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        }

        [Fact, DisplayName("Decisão do gate persiste e é única por Fluxo + Ambiente + Momento + pipeline")]
        public async Task DecisaoUnica()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            var avaliacao = new AvaliacaoDoGate(DecisaoGate.Bloqueado, new ContagemDeSeveridade(1, 2, 3, 4), MotivosDoGate.Bloqueado);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                ctx.Add(DecisaoDoGateEntity.Criar(c.Fluxo().Id, AmbienteDeploy.Qa, MomentoDoGate.Gate, avaliacao, 9, T0));
                await ctx.SaveChangesAsync(Ct);
            }

            await using var outro = ContextoDoCadastro.Novo(infra);
            outro.Add(DecisaoDoGateEntity.Criar(c.Fluxo().Id, AmbienteDeploy.Qa, MomentoDoGate.Gate, avaliacao, 9, T0));
            var act = () => outro.SaveChangesAsync(Ct);

            await act.Should().ThrowAsync<DbUpdateException>();
        }

        [Fact, DisplayName("EtapaFluxo bloqueada persiste como 'bloqueada' com o motivo em CodigoFalha")]
        public async Task EtapaBloqueada()
        {
            var c = await CenarioDeEntrega.CriarAsync(infra);
            await using (var ctx = ContextoDoCadastro.Novo(infra))
            {
                var fluxo = await ctx.Set<FluxoEntity>().Include(f => f.Etapas).SingleAsync(f => f.Id == c.Fluxo().Id, Ct);
                fluxo.Etapa(EtapaDoFluxo.Qa).IniciarExecucao(5, T0).IsSuccess.Should().BeTrue();
                fluxo.Etapa(EtapaDoFluxo.Qa).Bloquear(MotivosDoGate.Bloqueado, T0.AddMinutes(1)).IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync(Ct);
            }

            await using var leitura = ContextoDoCadastro.Novo(infra);
            var bruto = await leitura.Database.SqlQuery<string>(
                $"SELECT \"Estado\" || '/' || \"CodigoFalha\" AS \"Value\" FROM esteira.\"EtapaFluxo\" WHERE \"FluxoId\" = {c.Fluxo().Id} AND \"Etapa\" = 'qa'").SingleAsync(Ct);
            bruto.Should().Be("bloqueada/gate.bloqueado");
        }
    }
}

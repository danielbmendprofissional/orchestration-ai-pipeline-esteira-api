using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Data.Entregas;
using Esteira.Integration.Tests.Aplicacoes;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Pipeline
{
    // Padrão fixo para testes que montam o AbrirPipelineHandler à mão.
    internal sealed class OpcoesAgentesFixas(decimal padrao = 2.00m) : IOpcoesDosAgentes
    {
        public decimal TetoCustoPadraoUsd => padrao;
    }

    // X3.3: leitura do teto pela porta `ITetoCustoDaAplicacaoQuery`, com PostgreSQL real.
    [Trait("Fatia", "X3")]
    public sealed class TetoCustoDaAplicacaoQueryTests(InfraestruturaFixture infra)
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact, DisplayName("Porta devolve o teto gravado na Aplicação e nulo quando não definido")]
        public async Task Porta_LeTeto()
        {
            var c = await CenarioDePipeline.CriarAsync(infra);
            await using (var escrita = ContextoDoCadastro.Novo(infra))
            {
                var app = await escrita.Set<Esteira.Domain.Aplicacoes.AplicacaoEntity>().FirstAsync(a => a.Id == c.Aplicacao.Id, Ct);
                app.DefinirTetoCusto(7.25m).IsSuccess.Should().BeTrue();
                await escrita.SaveChangesAsync(Ct);
            }

            await using var leitura = ContextoDoCadastro.NovoLeitura(infra);
            var porta = new TetoCustoDaAplicacaoQuery(leitura);

            (await porta.ObterAsync(c.Aplicacao.Id, Ct)).Should().Be(7.25m);

            var outra = await CenarioDePipeline.CriarAsync(infra);
            (await porta.ObterAsync(outra.Aplicacao.Id, Ct)).Should().BeNull();
        }
    }
}

using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Analises;
using Esteira.Data.Analises;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Esteira.Integration.Tests.Analises
{
    // E2 (spec 03-analises-e-gate.md, "Dados"): mapeamento e migration expand-only. Não precisa de container
    // (inspeciona o modelo do EF e as operações da migration).
    [Trait("Fatia", "E2")]
    public class AnalisesModeloTests
    {
        private static readonly string[] Tabelas = ["Analise", "Achado", "DecisaoDoGate"];

        private static Migration Migracao()
            => (Migration)Activator.CreateInstance(
                typeof(EsteiraContext).Assembly.GetTypes().Single(t => t.Name == "Analises_E2" && typeof(Migration).IsAssignableFrom(t)))!;

        private static EsteiraReadContext Contexto()
            => new(new DbContextOptionsBuilder<EsteiraReadContext>().UsarPostgres("Host=localhost;Database=x").Options);

        [Fact, DisplayName("A migration só cria Analise, Achado e DecisaoDoGate (com índices): nada existente é alterado nem removido")]
        public void ExpandOnly()
        {
            var operacoes = Migracao().UpOperations;

            operacoes.OfType<CreateTableOperation>().Select(o => o.Name).Should().BeEquivalentTo(Tabelas);
            operacoes.Should().OnlyContain(o => o is CreateTableOperation || o is CreateIndexOperation || o is EnsureSchemaOperation);
            operacoes.OfType<CreateTableOperation>().Should().OnlyContain(o => o.Schema == "esteira");
            operacoes.OfType<CreateTableOperation>().SelectMany(o => o.ForeignKeys).Should().OnlyContain(fk => fk.PrincipalSchema == "esteira");
        }

        [Fact, DisplayName("Índice único da Análise: Fluxo + Ambiente + Escopo + Fonte + Destino (nulo = '') + pipeline")]
        public void IndiceUnicoDaAnalise()
        {
            using var ctx = Contexto();
            var indice = ctx.Model.FindEntityType(typeof(AnaliseEntity))!.GetIndexes().Single(i => i.IsUnique && i.Properties.Count > 1);

            indice.Properties.Select(p => p.Name).Should().Equal(
                nameof(AnaliseEntity.FluxoId), nameof(AnaliseEntity.Ambiente), nameof(AnaliseEntity.Escopo),
                nameof(AnaliseEntity.Fonte), nameof(AnaliseEntity.DestinoNome), nameof(AnaliseEntity.PipelineGitLabId));
            indice.FindAnnotation("Npgsql:NullsDistinct")?.Value.Should().Be(false);
        }

        [Fact, DisplayName("Índice único da DecisaoDoGate: Fluxo + Ambiente + Momento + pipeline")]
        public void IndiceUnicoDaDecisao()
        {
            using var ctx = Contexto();
            var indice = ctx.Model.FindEntityType(typeof(DecisaoDoGateEntity))!.GetIndexes().Single(i => i.IsUnique && i.Properties.Count > 1);

            indice.Properties.Select(p => p.Name).Should().Equal(
                nameof(DecisaoDoGateEntity.FluxoId), nameof(DecisaoDoGateEntity.Ambiente), nameof(DecisaoDoGateEntity.Momento), nameof(DecisaoDoGateEntity.PipelineGitLabId));
        }

        [Fact, DisplayName("Enums gravados como texto kebab-case (ambiente producao, escopo artefato, severidade critica)")]
        public void EnumsComoTexto()
        {
            using var ctx = Contexto();
            var analise = ctx.Model.FindEntityType(typeof(AnaliseEntity))!;
            var achado = ctx.Model.FindEntityType(typeof(AchadoEntity))!;

            analise.FindProperty(nameof(AnaliseEntity.Ambiente))!.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string));
            analise.FindProperty(nameof(AnaliseEntity.Escopo))!.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string));
            analise.FindProperty(nameof(AnaliseEntity.Tipo))!.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string));
            analise.FindProperty(nameof(AnaliseEntity.Veredito))!.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string));
            achado.FindProperty(nameof(AchadoEntity.Severidade))!.GetValueConverter()!.ProviderClrType.Should().Be(typeof(string));
            achado.FindProperty(nameof(AchadoEntity.ChaveDoTipo))!.GetMaxLength().Should().Be(300);
        }

        [Fact, DisplayName("Nesta fatia ninguém tem Exceção: IExcecoesVigentesQuery devolve conjunto vazio")]
        public async Task SemExcecoes()
        {
            IExcecoesVigentesQuery query = new SemExcecoesVigentesQuery();

            var chaves = await query.ObterChavesCobertasAsync(1, AmbienteDeploy.Qa, TestContext.Current.CancellationToken);

            chaves.Should().BeEmpty();
        }
    }
}

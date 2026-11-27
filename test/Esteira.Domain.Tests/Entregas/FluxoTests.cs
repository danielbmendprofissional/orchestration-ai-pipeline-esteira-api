using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, critérios 4/9 e 14): SHA do Fluxo e feature_flags por Ambiente.
    [Trait("Fatia", "E1")]
    public class FluxoTests
    {
        private const string ShaA = "0123456789abcdef0123456789abcdef01234567";
        private const string ShaB = "fedcba9876543210fedcba9876543210fedcba98";
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        private static FluxoEntity Novo(params FatiaDaEntrega[] fatias)
        {
            var app = AplicacaoEntity.Criar("App", "APP", 1, "g").Value;
            var projeto = ProjetoEntity.Criar(app.Id, 10, "g/p", "p", Stack.Dotnet);
            var pessoa = PessoaEntity.Criar("sub", "F", "f@x.com", T0);
            return EntregaEntity.Abrir(app, pessoa, [new FluxoDaEntrega(projeto, true, TipoMudanca.Minor, fatias)], T0).Value.Fluxos.Single();
        }

        [Fact, DisplayName("O primeiro SHA registra no Fluxo; a Versao continua nula")]
        public void PrimeiroSha()
        {
            var f = Novo(new FatiaDaEntrega("B2", true));
            f.RegistrarSha(ShaA).IsSuccess.Should().BeTrue();
            (f.Sha, f.Versao).Should().Be((ShaA, null));
        }

        [Fact, DisplayName("O mesmo SHA de novo é idempotente")]
        public void ShaIgual()
        {
            var f = Novo(new FatiaDaEntrega("B2", true));
            f.RegistrarSha(ShaA);
            f.RegistrarSha(ShaA).IsSuccess.Should().BeTrue();
            f.Sha.Should().Be(ShaA);
        }

        [Fact, DisplayName("SHA diferente do registrado -> pipeline.sha-diferente (Conflict) e o Fluxo não muda")]
        public void ShaDiferente()
        {
            var f = Novo(new FatiaDaEntrega("B2", true));
            f.RegistrarSha(ShaA);

            var erro = f.RegistrarSha(ShaB).Errors.Should().ContainSingle().Subject;

            (erro.Code, erro.Type).Should().Be(("pipeline.sha-diferente", TipoErro.Conflict));
            f.Sha.Should().Be(ShaA);
        }

        [Theory, DisplayName("SHA fora de 40 hex minúsculo -> pipeline.sha-invalido")]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("0123456789ABCDEF0123456789ABCDEF01234567")]
        [InlineData("zz23456789abcdef0123456789abcdef01234567")]
        public void ShaInvalido(string sha)
        {
            var f = Novo(new FatiaDaEntrega("B2", true));
            f.RegistrarSha(sha).Errors.Should().ContainSingle().Which.Code.Should().Be("pipeline.sha-invalido");
            f.Sha.Should().BeNull();
        }

        [Theory, DisplayName("Critério 14: qa e hml devolvem =true, produção =false, só Fatias com flag")]
        [InlineData(AmbienteDeploy.Qa, "B2=true,B3=true")]
        [InlineData(AmbienteDeploy.Hml, "B2=true,B3=true")]
        [InlineData(AmbienteDeploy.Producao, "B2=false,B3=false")]
        public void FeatureFlags(AmbienteDeploy ambiente, string esperado)
        {
            var f = Novo(new FatiaDaEntrega("B3", true), new FatiaDaEntrega("A1", false), new FatiaDaEntrega("B2", true));
            f.FeatureFlags(ambiente).Should().Be(esperado);
        }

        [Fact, DisplayName("Critério 14: Fluxo sem nenhuma Fatia com flag devolve texto vazio")]
        public void SemFlag()
            => Novo(new FatiaDaEntrega("A1", false)).FeatureFlags(AmbienteDeploy.Producao).Should().BeEmpty();
    }
}

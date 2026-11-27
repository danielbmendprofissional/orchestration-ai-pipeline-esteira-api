using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Entregas;

namespace Esteira.Domain.Tests.Entregas
{
    // E1 (spec 01-entrega-e-fluxo.md, critério 3 e "Dados"): validação de Entrega.Abrir.
    [Trait("Fatia", "E1")]
    public class EntregaAbrirTests
    {
        private static readonly DateTime T0 = new(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        private static readonly AplicacaoEntity App = AplicacaoEntity.Criar("App", "APP", 1, "g").Value;
        private static readonly PessoaEntity Pessoa = PessoaEntity.Criar("sub", "Fulano", "f@x.com", T0);

        private static ProjetoEntity Projeto(long aplicacaoId = 0, bool ativo = true)
        {
            var p = ProjetoEntity.Criar(aplicacaoId, Random.Shared.NextInt64(1, 1_000_000), "g/p", "p", Stack.Dotnet);
            if (!ativo) p.Alterar(Stack.Dotnet, false);
            return p;
        }

        private static FluxoDaEntrega Fluxo(ProjetoEntity? projeto = null, bool qa = true, params FatiaDaEntrega[] fatias)
            => new(projeto ?? Projeto(), qa, TipoMudanca.Minor, fatias.Length == 0 ? [new FatiaDaEntrega("B2", true)] : fatias);

        private static Result<EntregaEntity> Abrir(params FluxoDaEntrega[] fluxos) => EntregaEntity.Abrir(App, Pessoa, fluxos, T0);

        [Fact, DisplayName("Deve abrir a Entrega aberta com um Fluxo por Projeto e as Fatias em maiúsculo")]
        public void Abre()
        {
            var r = Abrir(Fluxo(null, true, new FatiaDaEntrega(" b2 ", true), new FatiaDaEntrega("B3", false)));

            r.IsSuccess.Should().BeTrue();
            var e = r.Value;
            (e.Situacao, e.AbertaEm, e.AplicacaoId).Should().Be((SituacaoEntrega.Aberta, T0, App.Id));
            e.Fluxos.Should().ContainSingle().Which.Fatias.Select(f => (f.CodigoFatia, f.ComFlag))
                .Should().BeEquivalentTo([("B2", true), ("B3", false)]);
        }

        [Fact, DisplayName("Critério 3: sem Fluxo ou Fluxo sem Fatia -> entrega.sem-fatias")]
        public void SemFatias()
        {
            Abrir().Errors.Should().ContainSingle().Which.Code.Should().Be("entrega.sem-fatias");
            Abrir(new FluxoDaEntrega(Projeto(), true, TipoMudanca.Minor, [])).Errors.Single().Code.Should().Be("entrega.sem-fatias");
        }

        [Fact, DisplayName("Critério 3: Projeto de outra Aplicação -> entrega.projeto-invalido")]
        public void OutraAplicacao()
            => Abrir(Fluxo(Projeto(aplicacaoId: 99))).Errors.Should().ContainSingle().Which.Code.Should().Be("entrega.projeto-invalido");

        [Fact, DisplayName("Critério 3: Projeto inativo -> entrega.projeto-invalido")]
        public void Inativo()
            => Abrir(Fluxo(Projeto(ativo: false))).Errors.Should().ContainSingle().Which.Code.Should().Be("entrega.projeto-invalido");

        [Fact, DisplayName("Critério 3: qa desabilitado no Projeto -> ambiente.desabilitado")]
        public void QaDesabilitado()
        {
            var erro = Abrir(Fluxo(qa: false)).Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("ambiente.desabilitado", TipoErro.Validation));
        }

        [Fact, DisplayName("Deve recusar dois Fluxos do mesmo Projeto")]
        public void FluxoDuplicado()
        {
            var p = Projeto();
            Abrir(Fluxo(p), Fluxo(p, true, new FatiaDaEntrega("B3", false))).Errors.Should().ContainSingle()
                .Which.Code.Should().Be("entrega.fluxo-duplicado");
        }

        [Fact, DisplayName("Deve recusar a mesma Fatia duas vezes no mesmo Fluxo (mesmo em caixa diferente)")]
        public void FatiaDuplicada()
            => Abrir(Fluxo(null, true, new FatiaDaEntrega("B2", true), new FatiaDaEntrega("b2", false))).Errors.Should().ContainSingle()
                .Which.Code.Should().Be("entrega.fatia-duplicada");

        [Fact, DisplayName("Deve recusar código de Fatia vazio")]
        public void FatiaVazia()
            => Abrir(Fluxo(null, true, new FatiaDaEntrega("  ", true))).Errors.Should().ContainSingle()
                .Which.Code.Should().Be("entrega.fatia-invalida");
    }
}

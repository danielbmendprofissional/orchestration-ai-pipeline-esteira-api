using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Fatias
{
    // P2.2, critério 9: Abrir review (Membro, Aprovador técnico, MOR) e Decidir Fatia (Aprovador técnico, MOR).
    [Trait("Fatia", "P2")]
    public class PapeisDeDecisaoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Theory, DisplayName("9: quem pode Abrir review é Membro, Aprovador técnico ou MOR; os outros papéis não")]
        [InlineData(PapelAplicacao.Membro, false, true)]
        [InlineData(PapelAplicacao.AprovadorTecnico, false, true)]
        [InlineData(PapelAplicacao.Analista, false, false)]
        [InlineData(PapelAplicacao.AprovadorNegocio, false, false)]
        [InlineData(PapelAplicacao.Publicador, false, false)]
        [InlineData(PapelAplicacao.Publicador, true, true)]
        public void AbrirReview(PapelAplicacao papel, bool mor, bool esperado)
            => new AcessoAplicacao(mor, [papel]).Pode(OperacaoAplicacao.AbrirReview).Should().Be(esperado);

        [Theory, DisplayName("9: quem pode decidir (Aprovar/Reprovar) é Aprovador técnico ou MOR; Membro, Analista, Aprovador de negócio e Publicador não")]
        [InlineData(PapelAplicacao.AprovadorTecnico, false, true)]
        [InlineData(PapelAplicacao.Membro, false, false)]
        [InlineData(PapelAplicacao.Analista, false, false)]
        [InlineData(PapelAplicacao.AprovadorNegocio, false, false)]
        [InlineData(PapelAplicacao.Publicador, false, false)]
        [InlineData(PapelAplicacao.Membro, true, true)]
        public void DecidirFatia(PapelAplicacao papel, bool mor, bool esperado)
            => new AcessoAplicacao(mor, [papel]).Pode(OperacaoAplicacao.DecidirFatia).Should().Be(esperado);

        [Fact, DisplayName("9: quem tem Membro e também Aprovador técnico decide")]
        public void MembroMaisAprovadorDecide()
            => new AcessoAplicacao(false, [PapelAplicacao.Membro, PapelAplicacao.AprovadorTecnico]).Pode(OperacaoAplicacao.DecidirFatia).Should().BeTrue();

        [Fact, DisplayName("9: sem papel e sem MOR é 404; com papel sem a operação é 403 acesso.papel-insuficiente")]
        public async Task NotFoundOuForbidden()
        {
            var usuario = Substitute.For<IUsuarioAtual>();
            usuario.Id.Returns("sub-1");
            usuario.EhMor.Returns(false);
            var query = Substitute.For<IAcessoAplicacaoQuery>();
            query.PapeisAsync(1, "sub-1", Arg.Any<CancellationToken>()).Returns(Array.Empty<PapelAplicacao>());
            query.PapeisAsync(2, "sub-1", Arg.Any<CancellationToken>()).Returns(new[] { PapelAplicacao.Analista });
            var autorizacao = new AutorizacaoAplicacao(usuario, query);

            var semPapel = await autorizacao.AutorizarAsync(1, OperacaoAplicacao.AbrirReview, Ct);
            var semOperacao = await autorizacao.AutorizarAsync(2, OperacaoAplicacao.AbrirReview, Ct);

            semPapel.Errors.Should().ContainSingle().Which.Type.Should().Be(TipoErro.NotFound);
            semOperacao.Errors.Should().ContainSingle().Which.Should().Match<Erro>(e => e.Code == "acesso.papel-insuficiente" && e.Type == TipoErro.Forbidden);
        }
    }
}

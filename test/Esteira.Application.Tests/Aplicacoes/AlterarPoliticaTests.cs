using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.AlterarPolitica;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.3, critérios 11 e 11a: PUT /aplicacoes/{id}/politicas/{ambiente}.
    [Trait("Fatia", "A1")]
    public class AlterarPoliticaTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _repo = Substitute.For<IAplicacaoRepositorio>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly Guid _appId = Guid.NewGuid();

        public AlterarPoliticaTests()
        {
            _acesso.ResolverIdAsync(_appId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarPolitica, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            foreach (var amb in Enum.GetValues<AmbientePolitica>())
                _repo.ObterPoliticaAsync(5, amb, Arg.Any<CancellationToken>()).Returns(PoliticaSeveridadeEntity.Padrao(5, amb));
        }

        private Task<Result<PoliticaDto>> Executar(string ambiente, int? c, int? a, int? m, int? b, string modo)
            => new AlterarPoliticaHandler(_acesso, _autorizacao, _repo, _auditoria)
                .Handle(new AlterarPoliticaCommand(_appId, ambiente, c, a, m, b, modo), Ct);

        [Fact, DisplayName("Critério 11: limite negativo é recusado (politica.limite-negativo); null é sem limite")]
        public async Task LimiteNegativo()
        {
            var negativo = await Executar("review", null, null, -1, null, "avisa");
            var semLimite = await Executar("review", null, null, null, null, "avisa");

            negativo.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.limite-negativo");
            semLimite.IsSuccess.Should().BeTrue();
        }

        [Theory, DisplayName("Critério 11a: afrouxar o piso em qa, hml e producao é recusado (politica.abaixo-do-minimo)")]
        [InlineData("qa", 1, 0, "bloqueia")]
        [InlineData("hml", 0, null, "bloqueia")]
        [InlineData("producao", 0, 0, "avisa")]
        public async Task AbaixoDoMinimo(string ambiente, int? critica, int? alta, string modo)
        {
            var r = await Executar(ambiente, critica, alta, null, null, modo);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("politica.abaixo-do-minimo");
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Critério 11a: endurecer é permitido (review bloqueando, Média com limite) e é auditado com antes/depois")]
        public async Task Endurecer()
        {
            var r = await Executar("review", 0, 0, 3, null, "bloqueia");

            r.IsSuccess.Should().BeTrue();
            (r.Value.Ambiente, r.Value.Modo, r.Value.Media).Should().Be(("review", "bloqueia", 3));
            _auditoria.Received(1).Registrar("PoliticaSeveridade", Arg.Any<Guid>(), AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização e não altera a política")]
        public async Task SemPermissao()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarPolitica, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar("review", null, null, null, null, "bloqueia");

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            (await _repo.ObterPoliticaAsync(5, AmbientePolitica.Review, Ct))!.Modo.Should().Be(ModoPolitica.Avisa);
        }

        [Fact, DisplayName("Aplicação inexistente responde aplicacao.nao-encontrada")]
        public async Task Inexistente()
        {
            var r = await new AlterarPoliticaHandler(_acesso, _autorizacao, _repo, _auditoria)
                .Handle(new AlterarPoliticaCommand(Guid.NewGuid(), "qa", 0, 0, null, null, "bloqueia"), Ct);

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }

        [Theory, DisplayName("Validação: ambiente e modo fora do vocabulário têm code estável")]
        [InlineData("dev", "bloqueia", "politica.ambiente-invalido")]
        [InlineData("qa", "talvez", "politica.modo-invalido")]
        public void Validacao(string ambiente, string modo, string codigo)
        {
            var r = new AlterarPoliticaValidator().Validate(new AlterarPoliticaCommand(_appId, ambiente, 0, 0, null, null, modo));

            r.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be(codigo);
        }
    }
}

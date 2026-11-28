using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.DefinirTetoCusto;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // X3.2: PUT /aplicacoes/{id}/teto-custo (só MOR) — critérios 1, 4, 6 e 11 no handler.
    [Trait("Fatia", "X3")]
    public class DefinirTetoCustoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _repo = Substitute.For<IAplicacaoRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly IOpcoesDosAgentes _opcoes = Substitute.For<IOpcoesDosAgentes>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;

        public DefinirTetoCustoTests()
        {
            _opcoes.TetoCustoPadraoUsd.Returns(2.00m);
            _acesso.ResolverIdAsync(_app.PublicId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarTetoCusto, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _repo.ObterAsync(5, Arg.Any<CancellationToken>()).Returns(_app);
        }

        private Task<Result<TetoCustoDto>> Executar(decimal? valor, Guid? id = null)
            => new DefinirTetoCustoHandler(_acesso, _autorizacao, _repo, _uow, _auditoria, _opcoes)
                .Handle(new DefinirTetoCustoCommand(id ?? _app.PublicId, valor), Ct);

        [Fact, DisplayName("Critério 1: MOR define o teto, recebe {teto, padrão, efetivo} e o efetivo é o definido")]
        public async Task Define()
        {
            var r = await Executar(2.50m);

            r.IsSuccess.Should().BeTrue();
            (r.Value.TetoCustoUsd, r.Value.PadraoUsd, r.Value.EfetivoUsd).Should().Be((2.50m, 2.00m, 2.50m));
            _app.TetoCustoAgentesUsd.Should().Be(2.50m);
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 1: null restaura o padrão (teto nulo, efetivo = padrão)")]
        public async Task Restaura()
        {
            _app.DefinirTetoCusto(5m);

            var r = await Executar(null);

            r.IsSuccess.Should().BeTrue();
            (r.Value.TetoCustoUsd, r.Value.PadraoUsd, r.Value.EfetivoUsd).Should().Be(((decimal?)null, 2.00m, 2.00m));
            _app.TetoCustoAgentesUsd.Should().BeNull();
        }

        [Fact, DisplayName("Critério 1: valor fora da faixa é recusado (aplicacao.teto-invalido), sem auditoria nem gravação")]
        public async Task Invalido()
        {
            var r = await Executar(0.05m);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.teto-invalido");
            _app.TetoCustoAgentesUsd.Should().BeNull();
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 6: grava auditoria Aplicacao/alterou com antes e depois do teto, antes do SaveChanges (mesma transação)")]
        public async Task Audita()
        {
            _app.DefinirTetoCusto(1m);

            await Executar(3m);

            Received.InOrder(() =>
            {
                _auditoria.Registrar("Aplicacao", _app.PublicId, AcaoAuditoria.Alterou,
                    Arg.Is<object?>(o => o != null && o.ToString()!.Contains("TetoCustoAgentesUsd = 1")),
                    Arg.Is<object?>(o => o != null && o.ToString()!.Contains("TetoCustoAgentesUsd = 3")));
                _uow.SaveChangesAsync();
            });
        }

        [Fact, DisplayName("Critério 4: pede a operação EditarTetoCusto e devolve o erro de quem não é MOR (403) sem alterar nada")]
        public async Task NaoMor()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarTetoCusto, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar(2.50m);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("acesso.papel-insuficiente");
            _app.TetoCustoAgentesUsd.Should().BeNull();
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact, DisplayName("Critério 4: sem papel e sem ser MOR responde aplicacao.nao-encontrada (404)")]
        public async Task SemPapel()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarTetoCusto, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.NaoEncontrada));

            var r = await Executar(2.50m);

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.nao-encontrada");
        }

        [Fact, DisplayName("Aplicação inexistente responde aplicacao.nao-encontrada")]
        public async Task Inexistente()
        {
            var r = await Executar(2.50m, id: Guid.NewGuid());

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.nao-encontrada");
        }

        [Fact, DisplayName("Critério 11: perdeu a corrida e o vencedor gravou outro valor -> 409 aplicacao.conflito-de-gravacao")]
        public async Task Corrida_OutroValor()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _repo.ObterTetoCustoGravadoAsync(5, Arg.Any<CancellationToken>()).Returns(4.00m);

            var r = await Executar(2.50m);

            var erro = r.Errors.Should().ContainSingle().Subject;
            (erro.Code, erro.Type).Should().Be(("aplicacao.conflito-de-gravacao", TipoErro.Conflict));
        }

        [Fact, DisplayName("Critério 11: perdeu a corrida mas o vencedor gravou o mesmo valor -> idempotente (sucesso)")]
        public async Task Corrida_MesmoValor()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _repo.ObterTetoCustoGravadoAsync(5, Arg.Any<CancellationToken>()).Returns(2.50m);

            var r = await Executar(2.50m);

            r.IsSuccess.Should().BeTrue();
            (r.Value.TetoCustoUsd, r.Value.EfetivoUsd).Should().Be((2.50m, 2.50m));
        }

        [Fact, DisplayName("Critério 11: perdeu a corrida pedindo o padrão (null) e o vencedor também limpou -> idempotente")]
        public async Task Corrida_MesmoValorNulo()
        {
            _uow.SaveChangesAsync().Returns<Task<int>>(_ => throw new ConcorrenciaOtimistaException());
            _repo.ObterTetoCustoGravadoAsync(5, Arg.Any<CancellationToken>()).Returns((decimal?)null);

            var r = await Executar(null);

            r.IsSuccess.Should().BeTrue();
            (r.Value.TetoCustoUsd, r.Value.EfetivoUsd).Should().Be(((decimal?)null, 2.00m));
        }
    }
}

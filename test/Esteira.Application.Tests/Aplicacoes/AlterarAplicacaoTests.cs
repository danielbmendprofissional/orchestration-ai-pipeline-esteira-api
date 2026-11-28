using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Aplicacoes.Commands.Alterar;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Auditoria;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // A1.3: PUT /aplicacoes/{id} (MOR) — nome, sigla, ativa.
    [Trait("Fatia", "A1")]
    public class AlterarAplicacaoTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private readonly IAcessoAplicacaoQuery _acesso = Substitute.For<IAcessoAplicacaoQuery>();
        private readonly IAutorizacaoAplicacao _autorizacao = Substitute.For<IAutorizacaoAplicacao>();
        private readonly IAplicacaoRepositorio _repo = Substitute.For<IAplicacaoRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private readonly IAuditoria _auditoria = Substitute.For<IAuditoria>();
        private readonly AplicacaoEntity _app = AplicacaoEntity.Criar("Valuto", "VLT", 42, "valuto").Value;
        private readonly Guid _publicId;

        public AlterarAplicacaoTests()
        {
            _publicId = _app.PublicId;
            _acesso.ResolverIdAsync(_publicId, Arg.Any<CancellationToken>()).Returns(5L);
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarAplicacao, Arg.Any<CancellationToken>()).Returns(Result.Sucesso());
            _repo.ObterAsync(5, Arg.Any<CancellationToken>()).Returns(_app);
        }

        private Task<Result<AplicacaoListaDto>> Executar(Guid? id = null, string sigla = "vlt2", bool? ativa = false)
            => new AlterarAplicacaoHandler(_acesso, _autorizacao, _repo, _uow, _auditoria)
                .Handle(new AlterarAplicacaoCommand(id ?? _publicId, "Valuto 2", sigla, ativa), Ct);

        [Fact, DisplayName("Altera nome, sigla (normalizada) e ativa, e audita antes/depois")]
        public async Task Altera()
        {
            var r = await Executar();

            r.IsSuccess.Should().BeTrue();
            (_app.Nome, _app.Sigla, _app.Ativa).Should().Be(("Valuto 2", "VLT2", false));
            r.Value.Sigla.Should().Be("VLT2");
            _auditoria.Received(1).Registrar("Aplicacao", _publicId, AcaoAuditoria.Alterou, Arg.Is<object?>(o => o != null), Arg.Is<object?>(o => o != null), Arg.Any<DadosDoRegistro>());
        }

        [Fact, DisplayName("Recusa sigla de outra Aplicação (aplicacao.sigla-duplicada), sem alterar nada")]
        public async Task SiglaDuplicada()
        {
            _repo.SiglaExisteAsync("VLT2", 5, Arg.Any<CancellationToken>()).Returns(true);

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.sigla-duplicada");
            _app.Nome.Should().Be("Valuto");
            _auditoria.DidNotReceiveWithAnyArgs().Registrar(default!, default, default, default, default, default!);
        }

        [Fact, DisplayName("Corrida: violação de unicidade vira aplicacao.sigla-duplicada")]
        public async Task Corrida()
        {
            _uow.SaveChangesAsync().Returns<int>(_ => throw new ViolacaoDeUnicidadeException("IX_Aplicacao_Sigla"));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Code.Should().Be("aplicacao.sigla-duplicada");
        }

        [Fact, DisplayName("Id inexistente responde aplicacao.nao-encontrada")]
        public async Task Inexistente()
        {
            var r = await Executar(id: Guid.NewGuid());

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.NaoEncontrada);
        }

        [Fact, DisplayName("Sem permissão devolve o erro da autorização (404 sem papel, 403 papel insuficiente)")]
        public async Task SemPermissao()
        {
            _autorizacao.AutorizarAsync(5, OperacaoAplicacao.EditarAplicacao, Arg.Any<CancellationToken>())
                .Returns(Result.Falha(AutorizacaoAplicacao.PapelInsuficiente));

            var r = await Executar();

            r.Errors.Should().ContainSingle().Which.Should().Be(AutorizacaoAplicacao.PapelInsuficiente);
            _app.Nome.Should().Be("Valuto");
        }

        [Fact, DisplayName("Validação: ativa é obrigatória, para não desativar por omissão")]
        public void AtivaObrigatoria()
        {
            var r = new AlterarAplicacaoValidator().Validate(new AlterarAplicacaoCommand(_publicId, "N", "S", null));

            r.Errors.Should().ContainSingle().Which.ErrorCode.Should().Be("aplicacao.ativa-obrigatoria");
        }
    }
}

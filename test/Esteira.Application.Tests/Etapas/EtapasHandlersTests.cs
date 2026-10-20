using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Etapas.Commands.AlterarStatusEtapa;
using Esteira.Application.Etapas.Commands.CriarEtapa;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.Etapas.Interfaces;
using Esteira.Domain.Etapas.Services;
using NSubstitute;

namespace Esteira.Application.Tests.Etapas
{
    public class EtapasHandlersTests
    {
        private readonly IEtapaRepository _repo = Substitute.For<IEtapaRepository>();

        [Theory, DisplayName("Deve criar a etapa na ordem seguinte à última cadastrada")]
        [InlineData(5, 6)]
        [InlineData(null, 1)]
        public async Task CriarEtapa_DeveCriarNaProximaOrdem(int? ultimaOrdem, int esperada)
        {
            _repo.ObterUltimaEtapa(Arg.Any<CancellationToken>())
                .Returns(ultimaOrdem is null ? null : EtapaEntity.Criar("ULT", "Última", ultimaOrdem, false));

            var resposta = await new CriarEtapaHandler(_repo)
                .Handle(new CriarEtapaCommand { Sigla = "NOVA", Descricao = "Nova", Obrigatorio = 'S' }, CancellationToken.None);

            resposta.Message.Should().Be("NOVA");
            await _repo.Received(1).AddAsync(
                Arg.Is<EtapaEntity>(e => e.Sigla == "NOVA" && e.Ordem == esperada && e.Obrigatorio),
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve lançar exceção ao criar etapa com sigla já existente")]
        public async Task CriarEtapa_SiglaExistente_DeveLancarExcecao()
        {
            _repo.ExisteSiglaAsync("DEV", Arg.Any<CancellationToken>()).Returns(true);

            var act = () => new CriarEtapaHandler(_repo).Handle(new CriarEtapaCommand { Sigla = "DEV", Descricao = "x", Obrigatorio = 'N' }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Já existe uma etapa com a sigla 'DEV'");
        }

        [Fact, DisplayName("Deve alterar o status da etapa")]
        public async Task AlterarStatus_DeveAlterar()
        {
            var etapa = EtapaEntity.Criar("DEV", "Desenvolvimento", 1, true);
            _repo.GetBySiglaAsync("DEV", Arg.Any<CancellationToken>()).Returns(etapa);

            await new AlterarStatusEtapaHandler(_repo, new EtapaService()).Handle(new AlterarStatusEtapaCommand("dev", "inativo"), CancellationToken.None);

            etapa.Status.Should().Be(StatusEnum.INATIVO);
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao alterar status de etapa inexistente")]
        public async Task AlterarStatus_EtapaInexistente_DeveLancarNotFound()
        {
            var act = () => new AlterarStatusEtapaHandler(_repo, new EtapaService()).Handle(new AlterarStatusEtapaCommand("XYZ", "ATIVO"), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }
    }
}

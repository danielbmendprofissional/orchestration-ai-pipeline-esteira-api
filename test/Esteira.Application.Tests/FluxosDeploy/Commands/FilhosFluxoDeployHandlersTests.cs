using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.DTOs.Request;
using Esteira.Application.FluxosDeploy.Commands.CancelarFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoNegocioFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarAprovacaoTecnicaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarGmudFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.CriarVersaoTarefasFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.RemoverAprovacaoNegocioFluxoDeploy;
using Esteira.Application.FluxosDeploy.Commands.RemoverCrsBdFluxoDeploy;
using Esteira.Application.Tests.Common;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.AprovadoresTecnicos.Interfaces;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Domain.FluxosDeploy.Entities;
using Esteira.Domain.FluxosDeploy.Events;
using Esteira.Domain.FluxosDeploy.Interfaces;
using NSubstitute;

namespace Esteira.Application.Tests.FluxosDeploy.Commands
{
    public class FilhosFluxoDeployHandlersTests
    {
        private readonly IFluxoDeployRepository _repo = Substitute.For<IFluxoDeployRepository>();
        private readonly IAprovadorTecnicoRepository _aprovadorRepo = Substitute.For<IAprovadorTecnicoRepository>();
        private readonly FluxoDeployEntity _fluxo = Construtor.Fluxo(1, "DEV", "HML");

        public FilhosFluxoDeployHandlersTests()
        {
            _repo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(_fluxo);
        }

        [Fact, DisplayName("Deve cancelar o fluxo e publicar o evento de cancelamento")]
        public async Task Cancelar_DeveCancelarFluxo()
        {
            var resposta = await new CancelarFluxoDeployHandler(_repo).Handle(new CancelarFluxoDeployCommand { Id = 1 }, CancellationToken.None);

            resposta.Message.Should().Be("Fluxo deploy cancelado com sucesso!");
            _fluxo.Status.Should().Be(StatusEnum.CANCELADO);
            _fluxo.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FluxoDeployCanceladoEvent>();
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao cancelar fluxo inexistente")]
        public async Task Cancelar_FluxoInexistente_DeveLancarNotFound()
        {
            var act = () => new CancelarFluxoDeployHandler(_repo).Handle(new CancelarFluxoDeployCommand { Id = 9 }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve vincular as tarefas de aprovação de negócio ao fluxo")]
        public async Task CriarAprovacaoNegocio_DeveVincularTarefas()
        {
            var command = new CriarAprovacaoNegocioFluxoDeployCommand
            {
                IdFluxoDeploy = 1,
                AprovacoesNegocio = new()
                {
                    new AprovacaoNegocioDtoRequest { Tarefa = 10, Url = "https://tarefas.exemplo/issues/10" },
                    new AprovacaoNegocioDtoRequest { Tarefa = 11, Url = "https://tarefas.exemplo/issues/11" }
                }
            };

            await new CriarAprovacaoNegocioFluxoDeployHandler(_repo).Handle(command, CancellationToken.None);

            _fluxo.AprovadoresNegocio.Select(a => a.IdTarefa).Should().Equal(10, 11);
        }

        [Fact, DisplayName("Deve desvincular a tarefa de aprovação de negócio")]
        public async Task RemoverAprovacaoNegocio_DeveDesvincular()
        {
            _fluxo.AdicionarAprovadorNegocio(10, "https://tarefas.exemplo/issues/10");

            var resposta = await new RemoverAprovacaoNegocioFluxoDeployHandler(_repo).Handle(new RemoverAprovacaoNegocioFluxoDeployCommand(1, 10), CancellationToken.None);

            resposta.Message.Should().Be("Tarefa 10 desvinculada do fluxo de deploy com sucesso!");
            _fluxo.AprovadoresNegocio.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao desvincular tarefa não vinculada")]
        public async Task RemoverAprovacaoNegocio_TarefaNaoVinculada_DeveLancarNotFound()
        {
            var act = () => new RemoverAprovacaoNegocioFluxoDeployHandler(_repo).Handle(new RemoverAprovacaoNegocioFluxoDeployCommand(1, 99), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Theory, DisplayName("Deve registrar a decisão técnica convertendo S/N para aprovado")]
        [InlineData('S', true)]
        [InlineData('N', false)]
        public async Task CriarAprovacaoTecnica_DeveRegistrarDecisao(char aprovado, bool esperado)
        {
            var fluxo = Construtor.Fluxo(2, "AET", "DEV");
            _repo.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(fluxo);
            var aprovador = Construtor.Aprovador(5);
            _aprovadorRepo.GetByUsuarioAsync("usuario5", Arg.Any<CancellationToken>()).Returns(aprovador);
            _aprovadorRepo.ObterPorEquipeAsync("INFRA", Arg.Any<CancellationToken>()).Returns(new[] { aprovador });

            var resposta = await new CriarAprovacaoTecnicaFluxoDeployHandler(_repo, _aprovadorRepo)
                .Handle(new CriarAprovacaoTecnicaFluxoDeployCommand(2, "usuario5", aprovado, "justificativa"), CancellationToken.None);

            resposta.Message.Should().Be("Aprovação da equipe INFRA realizada com sucesso!");
            fluxo.AprovadoresTecnicos.Should().ContainSingle(a => a.IdAprovadorTecnico == 5 && a.Aprovado == esperado);
        }

        [Fact, DisplayName("Deve impedir aprovação técnica fora da etapa de aprovação técnica")]
        public async Task CriarAprovacaoTecnica_ForaDaEtapaAet_DeveLancarExcecao()
        {
            var aprovador = Construtor.Aprovador(5);
            _aprovadorRepo.GetByUsuarioAsync("usuario5", Arg.Any<CancellationToken>()).Returns(aprovador);
            _aprovadorRepo.ObterPorEquipeAsync("INFRA", Arg.Any<CancellationToken>()).Returns(new[] { aprovador });

            var act = () => new CriarAprovacaoTecnicaFluxoDeployHandler(_repo, _aprovadorRepo)
                .Handle(new CriarAprovacaoTecnicaFluxoDeployCommand(1, "usuario5", 'S', null), CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("A aprovação técnica só pode ser registrada com a etapa de aprovação técnica em execução.");
            _fluxo.AprovadoresTecnicos.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar NotFoundException quando o usuário não for aprovador técnico")]
        public async Task CriarAprovacaoTecnica_UsuarioNaoAprovador_DeveLancarNotFound()
        {
            _aprovadorRepo.GetByUsuarioAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AprovadorTecnicoEntity)null);

            var act = () => new CriarAprovacaoTecnicaFluxoDeployHandler(_repo, _aprovadorRepo)
                .Handle(new CriarAprovacaoTecnicaFluxoDeployCommand(1, "x", 'S', null), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve vincular os CRS ao fluxo")]
        public async Task CriarCrsBd_DeveVincular()
        {
            var command = new CriarCrsBdFluxoDeployCommand
            {
                IdFluxoDeploy = 1,
                CrsBd = new() { new BdCrsDtoRequest { Crs = "CRS-1", Url = "https://tarefas.exemplo/issues/1" } }
            };

            await new CriarCrsBdFluxoDeployHandler(_repo).Handle(command, CancellationToken.None);

            _fluxo.Bds.Should().ContainSingle(b => b.Crs == "CRS-1");
        }

        [Fact, DisplayName("Deve desvincular o CRS e retornar o nome na mensagem")]
        public async Task RemoverCrsBd_DeveDesvincular()
        {
            _fluxo.AdicionarBd("CRS-1", "https://tarefas.exemplo/issues/1");
            _fluxo.Bds.Single().ComId(3);

            var resposta = await new RemoverCrsBdFluxoDeployHandler(_repo).Handle(new RemoverCrsBdFluxoDeployCommand { IdFluxoDeploy = 1, IdCrs = 3 }, CancellationToken.None);

            resposta.Message.Should().Be("CRS-1 desvinculado do fluxo de deploy com sucesso!");
            _fluxo.Bds.Should().BeEmpty();
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao desvincular CRS não vinculado")]
        public async Task RemoverCrsBd_NaoVinculado_DeveLancarNotFound()
        {
            var act = () => new RemoverCrsBdFluxoDeployHandler(_repo).Handle(new RemoverCrsBdFluxoDeployCommand { IdFluxoDeploy = 1, IdCrs = 3 }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Theory, DisplayName("Deve registrar a GMUD ou a justificativa com a mensagem adequada")]
        [InlineData(123, null, "GMUD #123 vinculada ao fluxo de deploy com sucesso!")]
        [InlineData(null, "sem gmud", "Justificativa cadastrada com sucesso para o fluxo de deploy!")]
        public async Task CriarGmud_DeveRegistrar(int? idGmud, string justificativa, string mensagem)
        {
            var resposta = await new CriarGmudFluxoDeployHandler(_repo, Construtor.ConfiguracaoTarefas).Handle(new CriarGmudFluxoDeployCommand(1, "usuario", justificativa, idGmud), CancellationToken.None);

            resposta.Message.Should().Be(mensagem);
            _fluxo.Gmud.Should().NotBeNull();
        }

        [Fact, DisplayName("Deve vincular a versão das tarefas ao fluxo")]
        public async Task CriarVersaoTarefas_DeveVincular()
        {
            await new CriarVersaoTarefasFluxoDeployHandler(_repo).Handle(new CriarVersaoTarefasFluxoDeployCommand(1, 2, 3), CancellationToken.None);

            _fluxo.VersaoTarefas.IdProjeto.Should().Be(2);
            _fluxo.VersaoTarefas.IdVersao.Should().Be(3);
        }
    }
}

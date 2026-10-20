using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Deploys.Interfaces;
using Esteira.Application.Deploys.Messages;
using Esteira.Application.Deploys.Queries.VerificarAnalisesDast;
using Esteira.Application.Deploys.Queries.ObterArtefatosAnalise;
using Esteira.Application.DTOs.Response;
using MassTransit;
using NSubstitute;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Application.Tests.Deploys.Queries
{
    public class DeployQueriesHandlersTests
    {
        private readonly IDeployQueryRepository _queryRepo = Substitute.For<IDeployQueryRepository>();
        private readonly IPublishEndpoint _bus = Substitute.For<IPublishEndpoint>();

        [Fact, DisplayName("Deve publicar uma mensagem de atualização para cada análise do scanner DAST em execução")]
        public async Task VerificarAnalisesDast_DevePublicarPorAnalise()
        {
            _queryRepo.ListarAnalisesDinamicasEmExecucaoAsync(Arg.Any<CancellationToken>()).Returns(new List<AnaliseEmExecucaoDto>
            {
                new() { IdAnalise = 1, ScanId = "a" },
                new() { IdAnalise = 2, ScanId = "b" }
            });

            await new VerificarAnalisesDastHandler(_queryRepo, _bus).Handle(new VerificarAnalisesDastQuery(), CancellationToken.None);

            await _bus.Received(1).Publish(Arg.Is<AtualizarAnaliseDastMessage>(m => m.IdAnalise == 1 && m.ScanId == "a"), Arg.Any<CancellationToken>());
            await _bus.Received(1).Publish(Arg.Is<AtualizarAnaliseDastMessage>(m => m.IdAnalise == 2 && m.ScanId == "b"), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve listar os artefatos do deploy com a paginação da requisição")]
        public async Task ObterArtefatosAnalise_DeveListar()
        {
            var pagination = new PaginacaoDto { Page = 1, PageSize = 1 };
            _queryRepo.ListarArtefatosAsync(3, pagination, Arg.Any<CancellationToken>())
                .Returns(new List<ArtefatoDto> { new() { Id = 1 } });
            var query = new ObterArtefatosAnaliseQuery { IdDeploy = 3, Pagination = pagination };

            var resposta = await new ObterArtefatosAnaliseHandler(_queryRepo).Handle(query, CancellationToken.None);

            resposta.Results.Should().ContainSingle();
            resposta.Pagination.Should().BeSameAs(pagination);
        }
    }
}

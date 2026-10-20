using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response;
using Esteira.Application.FluxosDeploy.Interfaces;
using Esteira.Application.FluxosDeploy.Queries.ObterCrsBdFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterGmudFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterListaFluxoDeploy;
using Esteira.Application.FluxosDeploy.Queries.ObterResumoFluxoDeploy;
using Esteira.Domain.Common.Enums;
using Esteira.Domain.Common.Exceptions;
using Esteira.Application.Settings;
using Microsoft.Extensions.Options;
using NSubstitute;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Application.Tests.FluxosDeploy.Queries
{
    public class FluxoDeployQueriesHandlersTests
    {
        private readonly IFluxoDeployQueryRepository _queryRepo = Substitute.For<IFluxoDeployQueryRepository>();
        private static readonly IOptions<GerenciadorTarefasSettings> Settings = Options.Create(new GerenciadorTarefasSettings { UrlNovaGmud = "https://tarefas.exemplo/gmud/nova" });

        private static List<TarefaDto> Tarefas(params int[] ids)
            => ids.Select(id => new TarefaDto { Id = id, Titulo = $"Tarefa {id}" }).ToList();

        [Fact, DisplayName("Deve listar os fluxos repassando filtro e a mesma paginação da requisição")]
        public async Task ObterLista_DeveRepassarFiltroEPaginacao()
        {
            var pagination = new PaginacaoDto { Page = 2, PageSize = 5, Order = "Inicio", SortOrder = "desc" };
            _queryRepo.ListarAsync(Arg.Any<FiltroFluxoDeploy>(), pagination, Arg.Any<CancellationToken>())
                .Returns(new List<FluxoDeployDto> { new() { Id = 1 } });
            var query = new ObterListaFluxoDeployQuery { Pagination = pagination, Sistema = "SIS", Status = StatusEnum.EXECUTANDO };

            var resposta = await new ObterListaFluxoDeployHandler(_queryRepo).Handle(query, CancellationToken.None);

            resposta.Results.Should().ContainSingle();
            resposta.Pagination.Should().BeSameAs(pagination);
            await _queryRepo.Received(1).ListarAsync(
                Arg.Is<FiltroFluxoDeploy>(f => f.Sistema == "SIS" && f.Status == StatusEnum.EXECUTANDO),
                pagination,
                Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve retornar o resumo somente de fluxo concluído")]
        public async Task ObterResumo_FluxoConcluido_DeveRetornarResumo()
        {
            _queryRepo.ObterStatusAsync(1, Arg.Any<CancellationToken>()).Returns(StatusEnum.CONCLUIDO);
            _queryRepo.ObterResumoAsync(1, Arg.Any<CancellationToken>()).Returns(new FluxoDeployResumoDto { IdFluxoDeploy = 1 });

            var resposta = await new ObterResumoFluxoDeployHandler(_queryRepo).Handle(new ObterResumoFluxoDeployQuery { Id = 1 }, CancellationToken.None);

            resposta.IdFluxoDeploy.Should().Be(1);
        }

        [Fact, DisplayName("Deve lançar exceção ao obter resumo de fluxo inexistente")]
        public async Task ObterResumo_FluxoInexistente_DeveLancarExcecao()
        {
            _queryRepo.ObterStatusAsync(1, Arg.Any<CancellationToken>()).Returns((StatusEnum?)null);

            var act = () => new ObterResumoFluxoDeployHandler(_queryRepo).Handle(new ObterResumoFluxoDeployQuery { Id = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Fluxo informado inexistente.");
        }

        [Fact, DisplayName("Deve lançar exceção ao obter resumo de fluxo não concluído")]
        public async Task ObterResumo_FluxoNaoConcluido_DeveLancarExcecao()
        {
            _queryRepo.ObterStatusAsync(1, Arg.Any<CancellationToken>()).Returns(StatusEnum.EXECUTANDO);

            var act = () => new ObterResumoFluxoDeployHandler(_queryRepo).Handle(new ObterResumoFluxoDeployQuery { Id = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>().WithMessage("Fluxo precisa estar concluído.");
            await _queryRepo.DidNotReceive().ObterResumoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact, DisplayName("Deve retornar a URL para criar GMUD quando o fluxo não tiver GMUD")]
        public async Task ObterGmud_SemGmud_DeveRetornarUrlPadrao()
        {
            _queryRepo.ObterGmudAsync(1, Arg.Any<CancellationToken>()).Returns((GmudDto)null);

            var resposta = await new ObterGmudFluxoDeployHandler(_queryRepo, Settings).Handle(new ObterGmudFluxoDeployQuery(1), CancellationToken.None);

            resposta.Url.Should().Be("https://tarefas.exemplo/gmud/nova");
        }

        [Fact, DisplayName("Deve retornar a GMUD cadastrada")]
        public async Task ObterGmud_ComGmud_DeveRetornarGmud()
        {
            var gmud = new GmudDto { Url = "https://tarefas.exemplo/issues/1" };
            _queryRepo.ObterGmudAsync(1, Arg.Any<CancellationToken>()).Returns(gmud);

            var resposta = await new ObterGmudFluxoDeployHandler(_queryRepo, Settings).Handle(new ObterGmudFluxoDeployQuery(1), CancellationToken.None);

            resposta.Should().BeSameAs(gmud);
        }

        [Fact, DisplayName("Deve lançar NotFoundException ao listar CRS de fluxo inexistente")]
        public async Task ObterCrsBd_FluxoInexistente_DeveLancarNotFound()
        {
            _queryRepo.ExisteAsync(1, Arg.Any<CancellationToken>()).Returns(false);

            var act = () => new ObterCrsBdFluxoDeployHandler(_queryRepo).Handle(new ObterCrsBdFluxoDeployQuery { IdFluxoDeploy = 1, Pagination = new PaginacaoDto() }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact, DisplayName("Deve listar os CRS do fluxo com a paginação da requisição")]
        public async Task ObterCrsBd_DeveListar()
        {
            var pagination = new PaginacaoDto();
            _queryRepo.ExisteAsync(1, Arg.Any<CancellationToken>()).Returns(true);
            _queryRepo.ListarCrsAsync(1, pagination, Arg.Any<CancellationToken>())
                .Returns(new List<BdCrsDto> { new() { Crs = "CRS-1" } });

            var resposta = await new ObterCrsBdFluxoDeployHandler(_queryRepo).Handle(new ObterCrsBdFluxoDeployQuery { IdFluxoDeploy = 1, Pagination = pagination }, CancellationToken.None);

            resposta.Results.Should().ContainSingle(c => c.Crs == "CRS-1");
            resposta.Pagination.Should().BeSameAs(pagination);
        }
    }
}

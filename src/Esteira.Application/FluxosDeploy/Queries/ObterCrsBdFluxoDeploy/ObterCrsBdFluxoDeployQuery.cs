using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esteira.Application.Common.Paginacao;
namespace Esteira.Application.FluxosDeploy.Queries.ObterCrsBdFluxoDeploy
{
    public class ObterCrsBdFluxoDeployQuery : IRequest<BaseListaResponseDto<BdCrsDto>>
    {
        public int IdFluxoDeploy { get; set; }
        public PaginacaoDto Pagination { get; set; }
    }
}

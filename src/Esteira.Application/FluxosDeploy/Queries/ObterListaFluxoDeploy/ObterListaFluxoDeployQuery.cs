using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using Esteira.Domain.Common.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esteira.Application.Common.Paginacao;
namespace Esteira.Application.FluxosDeploy.Queries.ObterListaFluxoDeploy
{
    public class ObterListaFluxoDeployQuery: IRequest<BaseListaResponseDto<FluxoDeployDto>>
    {
        public PaginacaoDto Pagination { get; set; } 
        public string Sistema { get; set; } 
        public DateOnly? DataInicio { get; set; } 
        public DateOnly? DataFim { get; set; } 
        public StatusEnum? Status { get; set; }
    }
}

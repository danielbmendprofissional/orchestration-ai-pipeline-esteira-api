using Esteira.Application.DTOs.Response;
using Esteira.Application.DTOs.Response.Common;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Esteira.Application.Common.Paginacao;

namespace Esteira.Application.Deploys.Queries.ObterArtefatosAnalise
{
    public class ObterArtefatosAnaliseQuery: IRequest<BaseListaResponseDto<ArtefatoDto>>
    {
        public PaginacaoDto Pagination { get; set; }
        public int IdDeploy { get; set; }
    }
}

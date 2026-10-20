using Esteira.Application.DTOs.Response;
using Esteira.Domain.Common.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Status.Queries.ObterStatus
{

    public class ObterStatusHandler
        : IRequestHandler<ObterStatusQuery, IEnumerable<OptionDto>>
    {   
        public Task<IEnumerable<OptionDto>> Handle(
            ObterStatusQuery request,            
            CancellationToken cancellationToken)
        {
            
            var result = Enum.GetValues<StatusEnum>()
                .Select(s => new OptionDto
                {
                    Codigo = s.ToString(),
                    Descricao = s.GetDescription()
                });

            return Task.FromResult(result);
        }
    }

}

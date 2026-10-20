using Esteira.Application.Deploys.Queries.VerificarAnalisesDast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using Quartz;

namespace Esteira.Application.Jobs
{
    public class AnaliseDastJob : IJob
    {
        private readonly IMediator _mediator;
        public AnaliseDastJob(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) 
           => await _mediator.Send(new VerificarAnalisesDastQuery(), cancellationToken);
        
    }
}

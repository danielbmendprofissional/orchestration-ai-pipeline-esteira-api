using Esteira.Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esteira.Application.Deploys.Queries.ObterArtefato
{

    public class ObterArtefatoQuery : IRequest<ArtefatoCompletoDto>
    {
        public int Id { get; set; }
    }

}

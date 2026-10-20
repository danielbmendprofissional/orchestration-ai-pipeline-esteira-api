using Esteira.Application.DTOs.Response;
using MediatR;

namespace Esteira.Application.Status.Queries.ObterStatus
{
    public class ObterStatusQuery : IRequest<IEnumerable<OptionDto>>
    {
    }
}

using Esteira.Application.Interfaces.Usuario;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.DTOs.Request;
using Esteira.Application.DTOs.Response.Common;
using MediatR;

namespace Esteira.Application.FluxosDeploy.Commands.CriarCrsBdFluxoDeploy
{
    public class CriarCrsBdFluxoDeployCommand : IRequest<BaseMessageResponseDto>, ICommand, IAlteraFluxoDeploy
    {
        public int IdFluxoDeploy { get; set; }
        public List<BdCrsDtoRequest> CrsBd { get; set; }
    }
}

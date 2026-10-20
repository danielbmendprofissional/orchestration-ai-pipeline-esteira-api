using Esteira.Application.DTOs.Response;
using Esteira.Application.Interfaces.Cache;
using MediatR;

namespace Esteira.Application.Etapas.Queries.ObterListaEtapa
{
    public class ObterListaEtapaQuery : IRequest<IEnumerable<EtapaDto>>, ICacheableQuery
    {
        public string ChaveCache => ChavesCache.EtapasAtivas;
        public TimeSpan DuracaoCache => TimeSpan.FromMinutes(10);
    }
}

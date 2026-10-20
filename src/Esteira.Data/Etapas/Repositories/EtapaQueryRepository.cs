using Esteira.Application.DTOs.Response;
using Esteira.Application.Etapas.Interfaces;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Etapas.Repositories
{
    public sealed class EtapaQueryRepository : IEtapaQueryRepository
    {
        private readonly EsteiraReadContext _context;

        public EtapaQueryRepository(EsteiraReadContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<EtapaDto>> ListarAtivasAsync(CancellationToken cancellationToken = default)
            => await _context.Etapas
                .Where(a => a.Status == StatusEnum.ATIVO)
                .OrderBy(a => a.Ordem)
                .Select(a => new EtapaDto
                {
                    Sigla = a.Sigla,
                    Descricao = a.Descricao,
                    Ordem = a.Ordem ?? 0,
                    Obrigatorio = a.Obrigatorio ? 'S' : 'N'
                })
                .ToListAsync(cancellationToken);
    }
}

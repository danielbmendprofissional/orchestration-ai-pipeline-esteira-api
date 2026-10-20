using Esteira.Application.AprovadoresTecnicos.Interfaces;
using Esteira.Application.DTOs.Response;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.AprovadoresTecnicos.Repositories
{
    public sealed class AprovadorTecnicoQueryRepository : IAprovadorTecnicoQueryRepository
    {
        private readonly EsteiraReadContext _context;

        public AprovadorTecnicoQueryRepository(EsteiraReadContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<EquipeDto>> ListarIntegrantesAtivosAsync(string equipe, CancellationToken cancellationToken = default)
        {
            var equipeNormalizada = equipe.ToUpper();

            return await _context.AprovadoresTecnicos
                .Where(a => a.Status == StatusEnum.ATIVO && a.Equipe.ToUpper() == equipeNormalizada)
                .Select(a => new EquipeDto
                {
                    Id = a.Id,
                    Usuario = a.Usuario,
                    Equipe = a.Equipe
                })
                .ToListAsync(cancellationToken);
        }
    }
}

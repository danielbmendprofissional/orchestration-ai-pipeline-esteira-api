using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class PessoaRepositorio(EsteiraContext contexto) : IPessoaRepositorio
    {
        public Task<PessoaEntity?> ObterPorSubAsync(string sub, CancellationToken cancellationToken = default)
            => contexto.Set<PessoaEntity>().FirstOrDefaultAsync(p => p.Sub == sub, cancellationToken);

        public Task<PessoaEntity?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
            => contexto.Set<PessoaEntity>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        public async Task AdicionarAsync(PessoaEntity pessoa, CancellationToken cancellationToken = default)
            => await contexto.Set<PessoaEntity>().AddAsync(pessoa, cancellationToken);
    }
}

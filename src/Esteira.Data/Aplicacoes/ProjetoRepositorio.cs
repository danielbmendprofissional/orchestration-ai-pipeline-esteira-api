using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Aplicacoes;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class ProjetoRepositorio(EsteiraContext contexto) : IProjetoRepositorio
    {
        public Task<ProjetoEntity?> ObterPorPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default)
            => contexto.Set<ProjetoEntity>().FirstOrDefaultAsync(p => p.PublicId == publicId, cancellationToken);

        public Task<bool> ProjetoGitLabCadastradoAsync(long projetoGitLabId, CancellationToken cancellationToken = default)
            => contexto.Set<ProjetoEntity>().AnyAsync(p => p.ProjetoGitLabId == projetoGitLabId, cancellationToken);

        public async Task<IReadOnlySet<long>> ProjetosGitLabCadastradosAsync(IReadOnlyCollection<long> projetoGitLabIds, CancellationToken cancellationToken = default)
        {
            if (projetoGitLabIds.Count == 0)
                return new HashSet<long>();

            var ids = projetoGitLabIds.ToArray();
            var encontrados = await contexto.Set<ProjetoEntity>()
                .Where(p => ids.Contains(p.ProjetoGitLabId))
                .Select(p => p.ProjetoGitLabId)
                .ToListAsync(cancellationToken);
            return encontrados.ToHashSet();
        }

        public async Task AdicionarAsync(ProjetoEntity projeto, CancellationToken cancellationToken = default)
            => await contexto.Set<ProjetoEntity>().AddAsync(projeto, cancellationToken);

        public async Task AdicionarAmbienteAsync(AmbienteProjetoEntity ambiente, CancellationToken cancellationToken = default)
            => await contexto.Set<AmbienteProjetoEntity>().AddAsync(ambiente, cancellationToken);

        public Task<AmbienteProjetoEntity?> ObterAmbienteAsync(long projetoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default)
            => contexto.Set<AmbienteProjetoEntity>().FirstOrDefaultAsync(a => a.ProjetoId == projetoId && a.Ambiente == ambiente, cancellationToken);

        public async Task<IReadOnlyList<ProjetoEntity>> ProjetosDaAplicacaoAsync(long aplicacaoId, CancellationToken cancellationToken = default)
            => await contexto.Set<ProjetoEntity>().Where(p => p.AplicacaoId == aplicacaoId).ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<DependenciaProjetoEntity>> DependenciasDaAplicacaoAsync(long aplicacaoId, CancellationToken cancellationToken = default)
        {
            var projetos = contexto.Set<ProjetoEntity>().Where(p => p.AplicacaoId == aplicacaoId).Select(p => p.Id);
            return await contexto.Set<DependenciaProjetoEntity>().Where(d => projetos.Contains(d.ProjetoId)).ToListAsync(cancellationToken);
        }

        public async Task AdicionarDependenciaAsync(DependenciaProjetoEntity dependencia, CancellationToken cancellationToken = default)
            => await contexto.Set<DependenciaProjetoEntity>().AddAsync(dependencia, cancellationToken);

        public void RemoverDependencia(DependenciaProjetoEntity dependencia)
            => contexto.Set<DependenciaProjetoEntity>().Remove(dependencia);

        public Task<DestinoEntity?> ObterDestinoAsync(long projetoId, AmbienteDeploy ambiente, Guid destinoPublicId, CancellationToken cancellationToken = default)
            => contexto.Set<DestinoEntity>().FirstOrDefaultAsync(
                d => d.ProjetoId == projetoId && d.Ambiente == ambiente && d.PublicId == destinoPublicId, cancellationToken);

        public Task<bool> DestinoNomeExisteAsync(long projetoId, AmbienteDeploy ambiente, string nome, long? exceto, CancellationToken cancellationToken = default)
            => contexto.Set<DestinoEntity>().AnyAsync(
                d => d.ProjetoId == projetoId && d.Ambiente == ambiente && d.Nome == nome && (exceto == null || d.Id != exceto), cancellationToken);

        public async Task AdicionarDestinoAsync(DestinoEntity destino, CancellationToken cancellationToken = default)
            => await contexto.Set<DestinoEntity>().AddAsync(destino, cancellationToken);

        public void RemoverDestino(DestinoEntity destino)
            => contexto.Set<DestinoEntity>().Remove(destino);

        public async Task<ProjetoResumoDto> MontarDtoAsync(ProjetoEntity projeto, CancellationToken cancellationToken = default)
        {
            var ambientes = await contexto.Set<AmbienteProjetoEntity>().Where(a => a.ProjetoId == projeto.Id).ToListAsync(cancellationToken);
            var destinos = await contexto.Set<DestinoEntity>().Where(d => d.ProjetoId == projeto.Id).ToListAsync(cancellationToken);

            // A resposta sai antes do commit: as dependências vêm do banco, ajustadas pelo que o comando já adicionou/removeu.
            var gravadas = await contexto.Set<DependenciaProjetoEntity>()
                .Where(d => d.ProjetoId == projeto.Id).Select(d => d.DependeDeProjetoId).ToListAsync(cancellationToken);
            var pendentes = contexto.ChangeTracker.Entries<DependenciaProjetoEntity>().Where(e => e.Entity.ProjetoId == projeto.Id).ToList();
            var alvos = gravadas
                .Except(pendentes.Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.DependeDeProjetoId))
                .Union(pendentes.Where(e => e.State == EntityState.Added).Select(e => e.Entity.DependeDeProjetoId))
                .ToList();
            var dependeDe = await contexto.Set<ProjetoEntity>()
                .Where(p => alvos.Contains(p.Id)).OrderBy(p => p.Nome).ThenBy(p => p.Id).Select(p => p.PublicId).ToListAsync(cancellationToken);

            return ProjetoErros.Dto(projeto, ambientes, destinos, dependeDe);
        }
    }
}

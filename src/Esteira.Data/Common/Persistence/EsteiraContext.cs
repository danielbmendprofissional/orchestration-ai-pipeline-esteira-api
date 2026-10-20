using Esteira.Application.Common.Events;
using Esteira.Data.AprovadoresTecnicos.Persistence.Configurations.Write;
using Esteira.Data.ConfiguracoesSistema.Persistence.Configurations.Write;
using Esteira.Data.Deploys.Persistence.Configurations.Write;
using Esteira.Data.Etapas.Persistence.Configurations.Write;
using Esteira.Data.FluxosDeploy.Persistence.Configurations.Write;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.Common.Base;
using Esteira.Domain.ConfiguracoesSistema.Entities;
using Esteira.Domain.Deploys.Entities;
using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence
{
    public sealed class EsteiraContext : DbContext
    {
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public EsteiraContext(
            DbContextOptions<EsteiraContext> options,
            IDomainEventDispatcher domainEventDispatcher)
            : base(options)
        {
            _domainEventDispatcher = domainEventDispatcher;
        }

        public DbSet<FluxoDeployEntity> FluxosDeploy => Set<FluxoDeployEntity>();
        public DbSet<DeployEntity> Deploys => Set<DeployEntity>();
        public DbSet<EtapaEntity> Etapas => Set<EtapaEntity>();
        public DbSet<AprovadorTecnicoEntity> AprovadoresTecnicos => Set<AprovadorTecnicoEntity>();
        public DbSet<ConfiguracaoSistemaEntity> ConfiguracoesSistema => Set<ConfiguracaoSistemaEntity>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
            configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new FluxoDeployConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployEtapaConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployBdCrsConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployAprovadorNegocioConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployAprovadorTecnicoConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployGmudConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployVersaoTarefasConfiguration());

            modelBuilder.ApplyConfiguration(new DeployConfiguration());
            modelBuilder.ApplyConfiguration(new AnaliseConfiguration());
            modelBuilder.ApplyConfiguration(new ArtefatoConfiguration());

            modelBuilder.ApplyConfiguration(new EtapaConfiguration());
            modelBuilder.ApplyConfiguration(new AprovadorTecnicoConfiguration());
            modelBuilder.ApplyConfiguration(new ConfiguracaoSistemaConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await DespacharEventosAsync(cancellationToken);

            return await base.SaveChangesAsync(cancellationToken);
        }

        private async Task DespacharEventosAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var raizes = ChangeTracker
                    .Entries<AggregateRoot>()
                    .Select(e => e.Entity)
                    .Where(e => e.DomainEvents.Count > 0)
                    .ToList();

                if (raizes.Count == 0)
                    return;

                var eventos = raizes
                    .SelectMany(r => r.DomainEvents)
                    .ToList();

                raizes.ForEach(r => r.LimparEventos());

                await _domainEventDispatcher.DispatchAsync(eventos, cancellationToken);
            }
        }
    }
}

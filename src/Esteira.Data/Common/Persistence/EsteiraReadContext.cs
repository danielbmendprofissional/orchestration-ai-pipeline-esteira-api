using Esteira.Data.AprovadoresTecnicos.Persistence.Configurations.Read;
using Esteira.Data.AprovadoresTecnicos.Persistence.QueryModels;
using Esteira.Data.Deploys.Persistence.Configurations.Read;
using Esteira.Data.Deploys.Persistence.QueryModels;
using Esteira.Data.Etapas.Persistence.Configurations.Read;
using Esteira.Data.Etapas.Persistence.QueryModels;
using Esteira.Data.FluxosDeploy.Persistence.Configurations.Read;
using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Common.Persistence
{
    public sealed class EsteiraReadContext : DbContext
    {
        public EsteiraReadContext(DbContextOptions<EsteiraReadContext> options)
            : base(options)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public DbSet<FluxoDeployQueryModel> FluxosDeploy => Set<FluxoDeployQueryModel>();
        public DbSet<FluxoDeployEtapaQueryModel> FluxosDeployEtapas => Set<FluxoDeployEtapaQueryModel>();
        public DbSet<FluxoDeployBdCrsQueryModel> FluxosDeployCrs => Set<FluxoDeployBdCrsQueryModel>();
        public DbSet<FluxoDeployAprovadorNegocioQueryModel> FluxosDeployAprovadoresNegocio => Set<FluxoDeployAprovadorNegocioQueryModel>();
        public DbSet<FluxoDeployAprovadorTecnicoQueryModel> FluxosDeployAprovadoresTecnicos => Set<FluxoDeployAprovadorTecnicoQueryModel>();
        public DbSet<FluxoDeployGmudQueryModel> FluxosDeployGmuds => Set<FluxoDeployGmudQueryModel>();
        public DbSet<FluxoDeployVersaoTarefasQueryModel> FluxosDeployVersoesTarefas => Set<FluxoDeployVersaoTarefasQueryModel>();
        public DbSet<FluxoDeployResumoQueryModel> FluxosDeployResumo => Set<FluxoDeployResumoQueryModel>();
        public DbSet<DeployQueryModel> Deploys => Set<DeployQueryModel>();
        public DbSet<AnaliseQueryModel> Analises => Set<AnaliseQueryModel>();
        public DbSet<ArtefatoQueryModel> Artefatos => Set<ArtefatoQueryModel>();
        public DbSet<EtapaQueryModel> Etapas => Set<EtapaQueryModel>();
        public DbSet<AprovadorTecnicoQueryModel> AprovadoresTecnicos => Set<AprovadorTecnicoQueryModel>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
            configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new FluxoDeployQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployEtapaQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployBdCrsQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployAprovadorNegocioQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployAprovadorTecnicoQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployGmudQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployVersaoTarefasQueryConfiguration());
            modelBuilder.ApplyConfiguration(new FluxoDeployResumoQueryConfiguration());

            modelBuilder.ApplyConfiguration(new DeployQueryConfiguration());
            modelBuilder.ApplyConfiguration(new AnaliseQueryConfiguration());
            modelBuilder.ApplyConfiguration(new ArtefatoQueryConfiguration());

            modelBuilder.ApplyConfiguration(new EtapaQueryConfiguration());
            modelBuilder.ApplyConfiguration(new AprovadorTecnicoQueryConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => throw new InvalidOperationException("O contexto de leitura não permite gravação.");

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("O contexto de leitura não permite gravação.");
    }
}

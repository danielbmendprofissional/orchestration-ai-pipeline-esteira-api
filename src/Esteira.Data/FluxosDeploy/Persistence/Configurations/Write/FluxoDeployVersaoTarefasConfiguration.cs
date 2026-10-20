using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployVersaoTarefasConfiguration : IEntityTypeConfiguration<FluxoDeployVersaoTarefasEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployVersaoTarefasEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_VERSAO_TAREFAS");
            builder.HasKey(a => a.IdFluxoDeploy);
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdProjeto).HasColumnName("COD_PROJETO");
            builder.Property(a => a.IdVersao).HasColumnName("COD_VERSAO");
        }
    }
}

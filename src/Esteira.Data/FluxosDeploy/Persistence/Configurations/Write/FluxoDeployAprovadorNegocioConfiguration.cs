using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployAprovadorNegocioConfiguration : IEntityTypeConfiguration<FluxoDeployAprovadorNegocioEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployAprovadorNegocioEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_APROV_NEG");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_APROVADOR_NEGOCIO");
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdTarefa).HasColumnName("COD_TAREFA");
            builder.Property(a => a.Url).HasColumnName("DSC_URL_TAREFA_APROVACAO").HasMaxLength(100).IsRequired(false);
        }
    }
}

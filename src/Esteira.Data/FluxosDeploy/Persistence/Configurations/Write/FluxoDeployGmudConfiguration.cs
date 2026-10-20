using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployGmudConfiguration : IEntityTypeConfiguration<FluxoDeployGmudEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployGmudEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_GMUD");
            builder.HasKey(a => a.IdFluxoDeploy);
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Url).HasColumnName("DSC_URL_GMUD").HasMaxLength(100).IsRequired(false);
            builder.Property(a => a.Justificativa).HasColumnName("DSC_JUSTIFICATIVA").HasMaxLength(4000).IsRequired(false);
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO").HasMaxLength(30).IsRequired(false);
            builder.Property(a => a.Execucao).HasColumnName("DAT_EXECUCAO");
        }
    }
}

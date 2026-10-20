using Esteira.Domain.Etapas.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployEtapaConfiguration : IEntityTypeConfiguration<FluxoDeployEtapaEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployEtapaEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_ETAPA");
            builder.HasKey(a => new { a.IdFluxoDeploy, a.SglEtapa });
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.SglEtapa).HasColumnName("SGL_ETAPA").HasMaxLength(10);
            builder.Property(a => a.Ordem).HasColumnName("NUM_ORDEM");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasMaxLength(15).HasConversion<string>();

            builder.HasOne<EtapaEntity>()
                .WithMany()
                .HasForeignKey(a => a.SglEtapa)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

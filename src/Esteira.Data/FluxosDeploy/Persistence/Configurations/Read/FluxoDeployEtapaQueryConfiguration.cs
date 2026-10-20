using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployEtapaQueryConfiguration : IEntityTypeConfiguration<FluxoDeployEtapaQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployEtapaQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_ETAPA");
            builder.HasKey(a => new { a.IdFluxoDeploy, a.SglEtapa });
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.SglEtapa).HasColumnName("SGL_ETAPA");
            builder.Property(a => a.Ordem).HasColumnName("NUM_ORDEM");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();

            builder.HasOne(a => a.Etapa)
                .WithMany()
                .HasForeignKey(a => a.SglEtapa);
        }
    }
}

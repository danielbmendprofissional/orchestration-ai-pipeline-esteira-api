using Esteira.Data.FluxosDeploy.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Read
{
    public sealed class FluxoDeployQueryConfiguration : IEntityTypeConfiguration<FluxoDeployQueryModel>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployQueryModel> builder)
        {
            builder.ToTable("FLUXO_DEPLOY");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Inicio).HasColumnName("DAT_INICIO");
            builder.Property(a => a.Fim).HasColumnName("DAT_FIM");
            builder.Property(a => a.Sistema).HasColumnName("SGL_SISTEMA");
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO_CRIACAO");
            builder.Property(a => a.Versao).HasColumnName("DSC_VERSAO");

            builder.HasMany(a => a.Etapas)
                .WithOne()
                .HasForeignKey(e => e.IdFluxoDeploy);
        }
    }
}

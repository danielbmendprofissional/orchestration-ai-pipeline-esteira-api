using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.AprovadoresTecnicos.Entities;
using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployAprovadorTecnicoConfiguration : IEntityTypeConfiguration<FluxoDeployAprovadorTecnicoEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployAprovadorTecnicoEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY_APROV_TEC");
            builder.HasKey(a => new { a.IdFluxoDeploy, a.IdAprovadorTecnico });
            builder.Property(a => a.IdFluxoDeploy).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.IdAprovadorTecnico).HasColumnName("ID_APROVADOR_TECNICO");
            builder.Property(a => a.Execucao).HasColumnName("DAT_EXECUCAO").IsRequired(false);
            builder.Property(a => a.Aprovado).HasColumnName("IND_APROVADO").HasMaxLength(1).HasConversion(new SimNaoConverter());
            builder.Property(a => a.Justificativa).HasColumnName("DSC_JUSTIFICATIVA").HasMaxLength(4000).IsRequired(false);

            builder.HasOne<AprovadorTecnicoEntity>()
                .WithMany()
                .HasForeignKey(a => a.IdAprovadorTecnico)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

using Esteira.Domain.FluxosDeploy.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.FluxosDeploy.Persistence.Configurations.Write
{
    public sealed class FluxoDeployConfiguration : IEntityTypeConfiguration<FluxoDeployEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoDeployEntity> builder)
        {
            builder.ToTable("FLUXO_DEPLOY");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_FLUXO_DEPLOY");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasMaxLength(15).HasConversion<string>();
            builder.Property(a => a.Inicio).HasColumnName("DAT_INICIO");
            builder.Property(a => a.Fim).HasColumnName("DAT_FIM");
            builder.Property(a => a.Sistema).HasColumnName("SGL_SISTEMA");
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO_CRIACAO").HasMaxLength(30);
            builder.Property(a => a.Versao).HasColumnName("DSC_VERSAO").HasMaxLength(30);

            builder.Ignore(a => a.DomainEvents);

            builder.HasMany(a => a.Etapas)
                .WithOne()
                .HasForeignKey(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.Bds)
                .WithOne()
                .HasForeignKey(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.AprovadoresNegocio)
                .WithOne()
                .HasForeignKey(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.AprovadoresTecnicos)
                .WithOne()
                .HasForeignKey(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.Gmud)
                .WithOne()
                .HasForeignKey<FluxoDeployGmudEntity>(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.VersaoTarefas)
                .WithOne()
                .HasForeignKey<FluxoDeployVersaoTarefasEntity>(e => e.IdFluxoDeploy)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(a => a.Etapas).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(a => a.Bds).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(a => a.AprovadoresNegocio).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(a => a.AprovadoresTecnicos).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}

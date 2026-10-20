using Esteira.Domain.AprovadoresTecnicos.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.AprovadoresTecnicos.Persistence.Configurations.Write
{
    public sealed class AprovadorTecnicoConfiguration : IEntityTypeConfiguration<AprovadorTecnicoEntity>
    {
        public void Configure(EntityTypeBuilder<AprovadorTecnicoEntity> builder)
        {
            builder.ToTable("APROVADOR_TECNICO");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_APROVADOR_TECNICO");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasMaxLength(15).HasConversion<string>();
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO_APROVADOR").HasMaxLength(30);
            builder.Property(a => a.Equipe).HasColumnName("DSC_EQUIPE_TECNICA").HasMaxLength(30);
        }
    }
}

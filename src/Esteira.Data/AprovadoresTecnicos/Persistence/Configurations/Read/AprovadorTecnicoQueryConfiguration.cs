using Esteira.Data.AprovadoresTecnicos.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.AprovadoresTecnicos.Persistence.Configurations.Read
{
    public sealed class AprovadorTecnicoQueryConfiguration : IEntityTypeConfiguration<AprovadorTecnicoQueryModel>
    {
        public void Configure(EntityTypeBuilder<AprovadorTecnicoQueryModel> builder)
        {
            builder.ToTable("APROVADOR_TECNICO");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).HasColumnName("ID_APROVADOR_TECNICO");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Usuario).HasColumnName("DSC_USUARIO_APROVADOR");
            builder.Property(a => a.Equipe).HasColumnName("DSC_EQUIPE_TECNICA");
        }
    }
}

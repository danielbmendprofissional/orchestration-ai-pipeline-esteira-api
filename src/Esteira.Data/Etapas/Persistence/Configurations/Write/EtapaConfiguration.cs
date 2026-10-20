using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Etapas.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Etapas.Persistence.Configurations.Write
{
    public sealed class EtapaConfiguration : IEntityTypeConfiguration<EtapaEntity>
    {
        public void Configure(EntityTypeBuilder<EtapaEntity> builder)
        {
            builder.ToTable("ETAPA");
            builder.HasKey(a => a.Sigla);
            builder.Property(a => a.Sigla).HasColumnName("SGL_ETAPA").HasMaxLength(10);
            builder.Property(a => a.Descricao).HasColumnName("DSC_ETAPA").HasMaxLength(50);
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Ordem).HasColumnName("NUM_ORDEM").IsRequired(false);
            builder.Property(a => a.Obrigatorio).HasColumnName("IND_OBRIGATORIO").HasMaxLength(1).HasConversion(new SimNaoConverter());
        }
    }
}

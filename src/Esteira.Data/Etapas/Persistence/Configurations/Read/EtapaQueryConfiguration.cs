using Esteira.Data.Common.Persistence.Converters;
using Esteira.Data.Etapas.Persistence.QueryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Etapas.Persistence.Configurations.Read
{
    public sealed class EtapaQueryConfiguration : IEntityTypeConfiguration<EtapaQueryModel>
    {
        public void Configure(EntityTypeBuilder<EtapaQueryModel> builder)
        {
            builder.ToTable("ETAPA");
            builder.HasKey(a => a.Sigla);
            builder.Property(a => a.Sigla).HasColumnName("SGL_ETAPA");
            builder.Property(a => a.Descricao).HasColumnName("DSC_ETAPA");
            builder.Property(a => a.Status).HasColumnName("IND_STATUS").HasConversion<string>();
            builder.Property(a => a.Ordem).HasColumnName("NUM_ORDEM");
            builder.Property(a => a.Obrigatorio).HasColumnName("IND_OBRIGATORIO").HasConversion(new SimNaoConverter());
        }
    }
}

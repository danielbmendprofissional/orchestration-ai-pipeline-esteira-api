using Esteira.Domain.ConfiguracoesSistema.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.ConfiguracoesSistema.Persistence.Configurations.Write
{
    public sealed class ConfiguracaoSistemaConfiguration : IEntityTypeConfiguration<ConfiguracaoSistemaEntity>
    {
        public void Configure(EntityTypeBuilder<ConfiguracaoSistemaEntity> builder)
        {
            builder.ToTable("CONFIGURACAO_SISTEMA");
            builder.HasKey(a => a.Sistema);
            builder.Property(a => a.Sistema).HasColumnName("SGL_SISTEMA").HasMaxLength(30);

            builder.OwnsOne(a => a.Limites, limites =>
            {
                limites.Property(l => l.VulnerabilidadeCritica).HasColumnName("NUM_VULNERABILIDADE_CRITICA");
                limites.Property(l => l.VulnerabilidadeAlta).HasColumnName("NUM_VULNERABILIDADE_ALTA");
                limites.Property(l => l.VulnerabilidadeMedia).HasColumnName("NUM_VULNERABILIDADE_MEDIA");
                limites.Property(l => l.VulnerabilidadeBaixa).HasColumnName("NUM_VULNERABILIDADE_BAIXA");
            });

            builder.Navigation(a => a.Limites).IsRequired();
        }
    }
}

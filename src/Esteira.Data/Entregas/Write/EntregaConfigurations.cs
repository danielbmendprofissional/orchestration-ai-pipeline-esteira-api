using Esteira.Data.Aplicacoes.Write;
using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Entregas.Write
{
    // E1 (spec 01-entrega-e-fluxo.md, "Dados"). Valem também no contexto de leitura (mesmas tabelas).
    internal sealed class EntregaConfiguration : IEntityTypeConfiguration<EntregaEntity>
    {
        public void Configure(EntityTypeBuilder<EntregaEntity> b)
        {
            b.ConfigurarBase("Entrega");
            b.Property(x => x.Situacao).HasConversion(new EnumKebabConverter<SituacaoEntrega>());
            b.HasIndex(x => new { x.AplicacaoId, x.Situacao });
            b.HasOne<AplicacaoEntity>().WithMany().HasForeignKey(x => x.AplicacaoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PessoaEntity>().WithMany().HasForeignKey(x => x.AbertaPorPessoaId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Fluxos).WithOne().HasForeignKey(f => f.EntregaId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Fluxos).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class FluxoConfiguration : IEntityTypeConfiguration<FluxoEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoEntity> b)
        {
            b.ConfigurarBase("Fluxo");
            b.Property(x => x.TipoMudanca).HasConversion(new EnumKebabConverter<TipoMudanca>());
            b.Property(x => x.Sha).HasMaxLength(40);
            b.Property(x => x.Versao);
            // Concorrência otimista (ADR-0009): a coluna de sistema xmin do PostgreSQL, sem coluna própria.
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            b.HasIndex(x => new { x.EntregaId, x.ProjetoId }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("CK_Fluxo_ShaHex", "\"Sha\" IS NULL OR \"Sha\" ~ '^[0-9a-f]{40}$'"));
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Fatias).WithOne().HasForeignKey(f => f.FluxoId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Fatias).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    internal sealed class FluxoFatiaConfiguration : IEntityTypeConfiguration<FluxoFatiaEntity>
    {
        public void Configure(EntityTypeBuilder<FluxoFatiaEntity> b)
        {
            b.ConfigurarBase("FluxoFatia");
            b.Property(x => x.CodigoFatia).IsRequired();
            b.HasIndex(x => new { x.FluxoId, x.CodigoFatia }).IsUnique();
        }
    }

    internal sealed class OcupacaoAmbienteConfiguration : IEntityTypeConfiguration<OcupacaoAmbienteEntity>
    {
        public void Configure(EntityTypeBuilder<OcupacaoAmbienteEntity> b)
        {
            b.ConfigurarBase("OcupacaoAmbiente");
            b.Property(x => x.Ambiente).HasConversion(new EnumKebabConverter<AmbienteDeploy>());
            // A invariante é do banco, não de consulta antes de gravar (ADR-0009). qa não tem ocupação.
            b.HasIndex(x => new { x.ProjetoId, x.Ambiente }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("CK_OcupacaoAmbiente_SoHmlEProducao", "\"Ambiente\" IN ('hml', 'producao')"));
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FluxoEntity>().WithMany().HasForeignKey(x => x.FluxoId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}

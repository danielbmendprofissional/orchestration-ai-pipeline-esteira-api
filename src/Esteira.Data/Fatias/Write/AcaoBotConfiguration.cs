using Esteira.Data.Aplicacoes.Write;
using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Fatias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Fatias.Write
{
    // P2 (spec 02-review-e-aprovacao.md, "Dados"). Tipo e Estado como texto kebab-case; chave de idempotência única.
    internal sealed class AcaoBotConfiguration : IEntityTypeConfiguration<AcaoBotEntity>
    {
        public void Configure(EntityTypeBuilder<AcaoBotEntity> b)
        {
            b.ConfigurarBase("AcaoBot");
            b.Property(x => x.FatiaCodigo).IsRequired();
            b.Property(x => x.Tipo).HasConversion(new EnumKebabConverter<TipoAcaoBot>());
            b.Property(x => x.Estado).HasConversion(new EnumKebabConverter<EstadoAcaoBot>());
            b.Property(x => x.ChaveIdempotencia).IsRequired();
            b.Property(x => x.Comentario).HasMaxLength(AcaoBotEntity.ComentarioMaximo);
            b.HasIndex(x => x.ChaveIdempotencia).IsUnique();
            b.HasIndex(x => new { x.AplicacaoId, x.FatiaCodigo });
            b.HasOne<AplicacaoEntity>().WithMany().HasForeignKey(x => x.AplicacaoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PessoaEntity>().WithMany().HasForeignKey(x => x.PedidaPorPessoaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AcaoBotEntity>().WithMany().HasForeignKey(x => x.DependeDeAcaoId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}

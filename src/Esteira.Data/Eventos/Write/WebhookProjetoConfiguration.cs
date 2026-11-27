using Esteira.Data.Aplicacoes.Write;
using Esteira.Data.Common.Persistence.Converters;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Eventos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esteira.Data.Eventos.Write
{
    // T1 (spec 01-tempo-real, "Dados"). Estado como texto kebab-case; uma linha por Projeto. Sem segredo no banco.
    internal sealed class WebhookProjetoConfiguration : IEntityTypeConfiguration<WebhookProjetoEntity>
    {
        public void Configure(EntityTypeBuilder<WebhookProjetoEntity> b)
        {
            b.ConfigurarBase("WebhookProjeto");
            b.Property(x => x.Estado).HasConversion(new EnumKebabConverter<EstadoWebhookProjeto>());
            b.Property(x => x.CodigoErro).HasMaxLength(200);
            b.HasIndex(x => x.ProjetoId).IsUnique();
            b.HasOne<ProjetoEntity>().WithMany().HasForeignKey(x => x.ProjetoId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}

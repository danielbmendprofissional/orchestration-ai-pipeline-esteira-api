using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    // Só inserção: sem métodos de alteração, setters privados.
    public sealed class RegistroAuditoriaEntity : EntityBase
    {
        private RegistroAuditoriaEntity() { }

        public DateTime Quando { get; private set; }
        public string Usuario { get; private set; } = default!;
        public string Entidade { get; private set; } = default!;
        public Guid EntidadePublicId { get; private set; }
        public AcaoAuditoria Acao { get; private set; }
        public string? Antes { get; private set; }
        public string? Depois { get; private set; }

        public static RegistroAuditoriaEntity Criar(
            DateTime quando, string usuario, string entidade, Guid entidadePublicId, AcaoAuditoria acao, string? antes, string? depois)
        {
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));
            Guard.AgainstNullOrWhiteSpace(entidade, nameof(entidade));
            Guard.AgainstInvalidEnum<AcaoAuditoria>(acao, nameof(acao));

            return new RegistroAuditoriaEntity
            {
                Quando = quando,
                Usuario = usuario,
                Entidade = entidade,
                EntidadePublicId = entidadePublicId,
                Acao = acao,
                Antes = antes,
                Depois = depois
            };
        }
    }
}

using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    // Só inserção: sem métodos de alteração, setters privados. O banco também recusa UPDATE/DELETE (X1).
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

        // X1: nulo = fora de qualquer Aplicação (só o MOR vê).
        public long? AplicacaoId { get; private set; }
        public OrigemAuditoria Origem { get; private set; }
        public string? UsuarioNome { get; private set; }
        public CategoriaAuditoria Categoria { get; private set; }
        public string? Verbo { get; private set; }
        public string? ObjetoCodigo { get; private set; }
        public string? ObjetoNome { get; private set; }
        public string? Motivo { get; private set; }
        public bool Autoaprovacao { get; private set; }

        public static RegistroAuditoriaEntity Criar(
            DateTime quando, string usuario, string entidade, Guid entidadePublicId, AcaoAuditoria acao, string? antes, string? depois,
            CategoriaAuditoria categoria = CategoriaAuditoria.Cadastro,
            OrigemAuditoria origem = OrigemAuditoria.Tela,
            long? aplicacaoId = null,
            string? usuarioNome = null,
            string? verbo = null,
            string? objetoCodigo = null,
            string? objetoNome = null,
            string? motivo = null,
            bool autoaprovacao = false)
        {
            Guard.AgainstNullOrWhiteSpace(usuario, nameof(usuario));
            Guard.AgainstNullOrWhiteSpace(entidade, nameof(entidade));
            Guard.AgainstInvalidEnum<AcaoAuditoria>(acao, nameof(acao));
            Guard.AgainstInvalidEnum<CategoriaAuditoria>(categoria, nameof(categoria));
            Guard.AgainstInvalidEnum<OrigemAuditoria>(origem, nameof(origem));

            return new RegistroAuditoriaEntity
            {
                Quando = quando,
                Usuario = usuario,
                Entidade = entidade,
                EntidadePublicId = entidadePublicId,
                Acao = acao,
                Antes = antes,
                Depois = depois,
                Categoria = categoria,
                Origem = origem,
                AplicacaoId = aplicacaoId,
                UsuarioNome = usuarioNome,
                Verbo = verbo,
                ObjetoCodigo = objetoCodigo,
                ObjetoNome = objetoNome,
                Motivo = motivo,
                Autoaprovacao = autoaprovacao
            };
        }
    }
}

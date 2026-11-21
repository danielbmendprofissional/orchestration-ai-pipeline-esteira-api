using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Validations;

namespace Esteira.Domain.Aplicacoes
{
    public sealed class ProjetoEntity : EntityBase
    {
        private ProjetoEntity() { }

        public long AplicacaoId { get; private set; }
        public long ProjetoGitLabId { get; private set; }
        public string Caminho { get; private set; } = default!;
        public string Nome { get; private set; } = default!;
        public Stack Stack { get; private set; }
        public bool Ativo { get; private set; }

        // Dados vêm do GitLab (não é digitação do usuário): valor inválido é erro de programação.
        public static ProjetoEntity Criar(long aplicacaoId, long projetoGitLabId, string caminho, string nome, Stack stack)
        {
            Guard.AgainstNullOrWhiteSpace(caminho, nameof(caminho));
            Guard.AgainstNullOrWhiteSpace(nome, nameof(nome));
            Guard.AgainstInvalidEnum<Stack>(stack, nameof(stack));

            return new ProjetoEntity
            {
                AplicacaoId = aplicacaoId,
                ProjetoGitLabId = projetoGitLabId,
                Caminho = caminho,
                Nome = nome,
                Stack = stack,
                Ativo = true
            };
        }

        public void Alterar(Stack stack, bool ativo)
        {
            Guard.AgainstInvalidEnum<Stack>(stack, nameof(stack));
            Stack = stack;
            Ativo = ativo;
        }
    }
}

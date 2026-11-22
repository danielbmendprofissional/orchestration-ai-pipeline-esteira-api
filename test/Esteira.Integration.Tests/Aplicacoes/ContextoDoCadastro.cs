using Esteira.Application.Common.Events;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Common.Events;
using Esteira.Integration.Tests.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Integration.Tests.Aplicacoes
{
    internal static class ContextoDoCadastro
    {
        public static EsteiraContext Novo(InfraestruturaFixture infra)
        {
            var opcoes = new DbContextOptionsBuilder<EsteiraContext>().UsarPostgres(infra.ConnectionString).Options;
            return new EsteiraContext(opcoes, new SemDespacho());
        }

        public static EsteiraReadContext NovoLeitura(InfraestruturaFixture infra)
        {
            var opcoes = new DbContextOptionsBuilder<EsteiraReadContext>().UsarPostgres(infra.ConnectionString).Options;
            return new EsteiraReadContext(opcoes);
        }

        // Sigla e grupo únicos por teste: o banco é compartilhado pelo assembly.
        public static string SiglaUnica() => $"T{Guid.NewGuid():N}"[..10];
        public static long IdUnico() => Random.Shared.NextInt64(1_000_000, long.MaxValue);

        private sealed class SemDespacho : IDomainEventDispatcher
        {
            public Task DispatchAsync(IEnumerable<IDomainEvent> eventos, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }

    internal sealed class UsuarioFalso(string id) : IUsuarioAtual
    {
        public string Id => id;
        public string Usuario => "usuario";
        public string Nome => "Usuário";
        public string Email => "u@x.com";
        public IReadOnlyList<string> Perfis => [];
        public bool EhMor => false;
    }
}

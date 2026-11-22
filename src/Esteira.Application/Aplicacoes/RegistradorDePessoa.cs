using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Aplicacoes
{
    public sealed class RegistradorDePessoa(IPessoaRepositorio pessoas, IUnitOfWork uow, TimeProvider relogio) : IRegistradorDePessoa
    {
        public async Task RegistrarAsync(string sub, string nome, string email, CancellationToken cancellationToken = default)
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var pessoa = await pessoas.ObterPorSubAsync(sub, cancellationToken);

            if (pessoa is null)
                await pessoas.AdicionarAsync(PessoaEntity.Criar(sub, nome, email, agora), cancellationToken);
            else
                pessoa.RegistrarLogin(nome, email, agora);

            await uow.SaveChangesAsync();
        }
    }
}

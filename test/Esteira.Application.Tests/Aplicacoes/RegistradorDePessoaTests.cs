using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Aplicacoes;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using NSubstitute;

namespace Esteira.Application.Tests.Aplicacoes
{
    // Critério 16 da A1 (parte do registro): Pessoa gravada ou atualizada a cada login.
    [Trait("Fatia", "A1")]
    public class RegistradorDePessoaTests
    {
        private readonly IPessoaRepositorio _pessoas = Substitute.For<IPessoaRepositorio>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
        private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => agora;
        }

        private readonly TimeProvider _relogio = new RelogioFixo(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));

        private RegistradorDePessoa Servico() => new(_pessoas, _uow, _relogio);

        [Fact, DisplayName("Primeiro login cria a Pessoa com sub, nome, e-mail e UltimoLoginEm UTC")]
        public async Task PrimeiroLogin_Cria()
        {
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns((PessoaEntity)null);

            await Servico().RegistrarAsync("sub-1", "Ana", "ana@x.com");

            await _pessoas.Received(1).AdicionarAsync(
                Arg.Is<PessoaEntity>(p => p.Sub == "sub-1" && p.Nome == "Ana" && p.Email == "ana@x.com" && p.UltimoLoginEm == _relogio.GetUtcNow().UtcDateTime),
                Arg.Any<CancellationToken>());
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact, DisplayName("Login seguinte atualiza nome, e-mail e UltimoLoginEm da Pessoa existente")]
        public async Task LoginSeguinte_Atualiza()
        {
            var existente = PessoaEntity.Criar("sub-1", "Velho", "velho@x.com", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            _pessoas.ObterPorSubAsync("sub-1", Arg.Any<CancellationToken>()).Returns(existente);

            await Servico().RegistrarAsync("sub-1", "Novo", "novo@x.com");

            existente.Nome.Should().Be("Novo");
            existente.Email.Should().Be("novo@x.com");
            existente.UltimoLoginEm.Should().Be(_relogio.GetUtcNow().UtcDateTime);
            await _pessoas.DidNotReceive().AdicionarAsync(Arg.Any<PessoaEntity>(), Arg.Any<CancellationToken>());
            await _uow.Received(1).SaveChangesAsync();
        }
    }
}

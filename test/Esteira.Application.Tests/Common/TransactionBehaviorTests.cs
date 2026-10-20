using System.ComponentModel;
using AwesomeAssertions;
using Esteira.Application.Behavios;
using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using NSubstitute;

namespace Esteira.Application.Tests.Common
{
    public class TransactionBehaviorTests
    {
        public sealed class Consulta { }
        public sealed class Comando : ICommand { }
        public sealed class ComandoTransacional : ITransactionRequest { }

        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        [Fact, DisplayName("Não deve gravar nada em consultas")]
        public async Task Consulta_NaoDeveGravar()
        {
            var resposta = await new TransactionBehavior<Consulta, string>(_uow).Handle(new Consulta(), _ => Task.FromResult("ok"), CancellationToken.None);

            resposta.Should().Be("ok");
            await _uow.DidNotReceive().SaveChangesAsync();
            await _uow.DidNotReceive().BeginTransactionAsync();
            await _uow.DidNotReceive().CommitAsync();
        }

        [Fact, DisplayName("Deve gravar as alterações depois de um comando sem abrir transação")]
        public async Task Comando_DeveGravar()
        {
            await new TransactionBehavior<Comando, string>(_uow).Handle(new Comando(), _ => Task.FromResult("ok"), CancellationToken.None);

            await _uow.Received(1).SaveChangesAsync();
            await _uow.DidNotReceive().BeginTransactionAsync();
        }

        [Fact, DisplayName("Deve abrir transação e confirmar ao final de um comando transacional")]
        public async Task ComandoTransacional_DeveConfirmar()
        {
            await new TransactionBehavior<ComandoTransacional, string>(_uow).Handle(new ComandoTransacional(), _ => Task.FromResult("ok"), CancellationToken.None);

            Received.InOrder(() =>
            {
                _uow.BeginTransactionAsync();
                _uow.CommitAsync();
            });
            await _uow.DidNotReceive().RollbackAsync();
        }

        [Fact, DisplayName("Deve desfazer a transação quando o comando transacional falhar")]
        public async Task ComandoTransacional_Falha_DeveDesfazer()
        {
            var act = () => new TransactionBehavior<ComandoTransacional, string>(_uow)
                .Handle(new ComandoTransacional(), _ => throw new InvalidOperationException("falha"), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            await _uow.Received(1).RollbackAsync();
            await _uow.DidNotReceive().CommitAsync();
        }
    }
}

using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using MediatR;

namespace Esteira.Application.Behavios
{
    public class TransactionBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IUnitOfWork _uow;

        public TransactionBehavior(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not ICommand)
                return await next();

            if (request is not ITransactionRequest)
            {
                var resposta = await next();
                await _uow.SaveChangesAsync();
                return resposta;
            }

            await _uow.BeginTransactionAsync();

            try
            {
                var response = await next();

                await _uow.CommitAsync();

                return response;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }
    }
}

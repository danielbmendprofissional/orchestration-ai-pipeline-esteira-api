using Esteira.Application.Interfaces.Transaction;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Common.Resultados;
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

                // ADR-0003: Result de falha não deixa rastro — nada é salvo.
                if (Falhou(resposta))
                    return resposta;

                await _uow.SaveChangesAsync();
                return resposta;
            }

            await _uow.BeginTransactionAsync();

            try
            {
                var response = await next();

                if (Falhou(response))
                {
                    await _uow.RollbackAsync();
                    return response;
                }

                await _uow.CommitAsync();

                return response;
            }
            catch
            {
                await _uow.RollbackAsync();
                throw;
            }
        }

        private static bool Falhou(TResponse resposta)
            => resposta is Result { IsSuccess: false };
    }
}

using Esteira.Application.Common.Extensions;
using Esteira.Application.Common.Paginacao;
using Microsoft.Extensions.Logging;

namespace Esteira.Application.Common.Base
{
    public class BaseHandler
    {
        private readonly ILogger _logger;

        public BaseHandler() { }

        public BaseHandler(ILogger logger)
        {
            _logger = logger;
        }

        protected void Paginacao<T>(ref List<T> query, PaginacaoDto pagination) where T : class
        {
            pagination.TotalItems = query.Count;

            query = query
                .AsQueryable()
                .OrdenarPor(pagination.Order, pagination.SortOrder)
                .Skip(pagination.Pular)
                .Take(pagination.Tamanho)
                .ToList();
        }

        protected void LogErrorException(Exception ex)
            => _logger?.LogError(ex, "Erro ao processar {Handler}: {Mensagem}", GetType().Name, ex.Message);
    }
}

using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class PessoaQuery(EsteiraReadContext contexto) : IPessoaQuery
    {
        private static readonly OrdenacaoPadrao<PessoaListaDto> Padrao =
            OrdenacaoPadrao<PessoaListaDto>.Por(p => p.Nome).Depois(p => p.Sub);

        public Task<Result<PaginaDto<PessoaListaDto>>> ListarAsync(PaginacaoDto paginacao, string? busca, CancellationToken cancellationToken = default)
        {
            var consulta = contexto.Set<PessoaEntity>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var padrao = $"%{Escapar(busca.Trim())}%";
                consulta = consulta.Where(p => EF.Functions.ILike(p.Nome, padrao, "\\") || EF.Functions.ILike(p.Email, padrao, "\\"));
            }

            return consulta.PaginarAsync(
                paginacao,
                p => new PessoaListaDto { Sub = p.Sub, Nome = p.Nome, Email = p.Email },
                Padrao,
                cancellationToken);
        }

        private static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}

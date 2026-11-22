using Esteira.Application.Aplicacoes;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Common.Paginacao;
using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Data.Common.Persistence;
using Esteira.Data.Common.Persistence.Extensions;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Aplicacoes
{
    public sealed class AplicacaoQuery(EsteiraReadContext contexto) : IAplicacaoQuery
    {
        private static readonly OrdenacaoPadrao<AplicacaoListaDto> Padrao =
            OrdenacaoPadrao<AplicacaoListaDto>.Por(a => a.Nome).Depois(a => a.Id);

        public Task<Result<PaginaDto<AplicacaoListaDto>>> ListarAsync(
            PaginacaoDto paginacao, string? busca, bool? ativa, string sub, bool ehMor, CancellationToken cancellationToken = default)
        {
            var projetos = contexto.Set<ProjetoEntity>();
            var papeis = contexto.Set<PapelAplicacaoEntity>();

            // MOR vê todas; os demais, só aquelas em que têm algum papel.
            var consulta = contexto.Set<AplicacaoEntity>()
                .Where(a => ehMor || papeis.Any(p => p.AplicacaoId == a.Id && p.Usuario == sub));

            if (ativa is not null)
                consulta = consulta.Where(a => a.Ativa == ativa);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var padrao = $"%{Escapar(busca.Trim())}%";
                consulta = consulta.Where(a =>
                    EF.Functions.ILike(a.Nome, padrao, "\\")
                    || EF.Functions.ILike(a.Sigla, padrao, "\\")
                    || EF.Functions.ILike(a.GrupoGitLabCaminho, padrao, "\\"));
            }

            return consulta.PaginarAsync(
                paginacao,
                a => new AplicacaoListaDto
                {
                    Id = a.PublicId,
                    Nome = a.Nome,
                    Sigla = a.Sigla,
                    Grupo = a.GrupoGitLabCaminho,
                    QuantidadeProjetos = projetos.Count(p => p.AplicacaoId == a.Id),
                    Ativa = a.Ativa
                },
                Padrao,
                cancellationToken);
        }

        public async Task<AplicacaoDetalheDto?> ObterDetalheAsync(long id, CancellationToken cancellationToken = default)
        {
            var app = await contexto.Set<AplicacaoEntity>().Where(a => a.Id == id).FirstOrDefaultAsync(cancellationToken);
            if (app is null)
                return null;

            var politicas = await contexto.Set<PoliticaSeveridadeEntity>().Where(p => p.AplicacaoId == id).ToListAsync(cancellationToken);
            var projetos = await contexto.Set<ProjetoEntity>().Where(p => p.AplicacaoId == id).OrderBy(p => p.Nome).ToListAsync(cancellationToken);
            var papeis = await contexto.Set<PapelAplicacaoEntity>().Where(p => p.AplicacaoId == id).OrderBy(p => p.Id).ToListAsync(cancellationToken);

            var ids = projetos.Select(p => p.Id).ToList();
            var ambientes = (await contexto.Set<AmbienteProjetoEntity>().Where(a => ids.Contains(a.ProjetoId)).ToListAsync(cancellationToken)).ToLookup(a => a.ProjetoId);
            var destinos = (await contexto.Set<DestinoEntity>().Where(d => ids.Contains(d.ProjetoId)).ToListAsync(cancellationToken)).ToLookup(d => d.ProjetoId);
            var publicIds = projetos.ToDictionary(p => p.Id, p => p.PublicId);
            var nomes = projetos.ToDictionary(p => p.Id, p => p.Nome);
            var dependencias = (await contexto.Set<DependenciaProjetoEntity>().Where(d => ids.Contains(d.ProjetoId)).ToListAsync(cancellationToken)).ToLookup(d => d.ProjetoId);

            return new AplicacaoDetalheDto
            {
                Id = app.PublicId,
                Nome = app.Nome,
                Sigla = app.Sigla,
                Grupo = app.GrupoGitLabCaminho,
                Ativa = app.Ativa,
                Politicas = politicas.OrderBy(p => p.Ambiente).Select(AplicacaoErros.Dto).ToList(),
                Projetos = projetos.Select(p => ProjetoErros.Dto(
                    p,
                    ambientes[p.Id],
                    destinos[p.Id],
                    dependencias[p.Id].OrderBy(d => nomes[d.DependeDeProjetoId], StringComparer.Ordinal).Select(d => publicIds[d.DependeDeProjetoId]))).ToList(),
                Papeis = await PapelQuery.ComPessoasAsync(contexto, papeis, cancellationToken)
            };
        }

        private static string Escapar(string texto) => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}

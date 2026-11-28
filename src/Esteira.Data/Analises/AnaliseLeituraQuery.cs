using Esteira.Application.Analises.Queries;
using Esteira.Application.Common.Extensions;
using Esteira.Application.Common.Paginacao;
using Esteira.Data.Common.Persistence;
using Esteira.Domain.Analises;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Entregas;
using Microsoft.EntityFrameworkCore;

namespace Esteira.Data.Analises
{
    // Leitura das Análises e Achados (EsteiraReadContext, sem tracking). Texto de terceiro sai como veio, sem log.
    public sealed class AnaliseLeituraQuery(EsteiraReadContext contexto) : IAnaliseLeituraQuery
    {
        public async Task<FluxoLocalizado?> LocalizarFluxoAsync(Guid entregaPublicId, Guid fluxoPublicId, CancellationToken cancellationToken = default)
        {
            var achado = await (
                from f in contexto.Set<FluxoEntity>()
                join e in contexto.Set<EntregaEntity>() on f.EntregaId equals e.Id
                where f.PublicId == fluxoPublicId && e.PublicId == entregaPublicId
                select new { e.AplicacaoId, FluxoId = f.Id })
                .FirstOrDefaultAsync(cancellationToken);
            return achado is null ? null : new FluxoLocalizado(achado.AplicacaoId, achado.FluxoId);
        }

        public async Task<AnalisesDaEtapaDto> AnalisesDaEtapaAsync(long fluxoId, AmbienteDeploy ambiente, CancellationToken cancellationToken = default)
        {
            var analises = contexto.Set<AnaliseEntity>().Where(a => a.FluxoId == fluxoId && a.Ambiente == ambiente);
            var ultima = await analises.MaxAsync(a => (long?)a.PipelineGitLabId, cancellationToken);
            if (ultima is null)
                return new AnalisesDaEtapaDto(null, []);

            var itens = await analises
                .Where(a => a.PipelineGitLabId == ultima)
                .OrderBy(a => a.Escopo).ThenBy(a => a.Fonte).ThenBy(a => a.DestinoNome).ThenBy(a => a.Id)
                .Select(a => new
                {
                    a.PublicId, a.Escopo, a.Fonte, a.Tipo, a.Veredito, a.DestinoNome, a.CustoUsd, a.RecebidaEm,
                    Critica = a.Achados.Count(x => x.Severidade == SeveridadeAchado.Critica),
                    Alta = a.Achados.Count(x => x.Severidade == SeveridadeAchado.Alta),
                    Media = a.Achados.Count(x => x.Severidade == SeveridadeAchado.Media),
                    Baixa = a.Achados.Count(x => x.Severidade == SeveridadeAchado.Baixa),
                    Info = a.Achados.Count(x => x.Severidade == SeveridadeAchado.Info)
                })
                .ToListAsync(cancellationToken);

            var decisao = await contexto.Set<DecisaoDoGateEntity>()
                .Where(d => d.FluxoId == fluxoId && d.Ambiente == ambiente && d.PipelineGitLabId == ultima)
                .OrderByDescending(d => d.DecididaEm).ThenByDescending(d => d.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return new AnalisesDaEtapaDto(
                decisao is null
                    ? null
                    : new DecisaoDaEtapaDto(
                        EnumKebab.Texto(decisao.Momento), EnumKebab.Texto(decisao.Decisao), decisao.Motivo,
                        new ContagemDto(decisao.Critica, decisao.Alta, decisao.Media, decisao.Baixa), decisao.DecididaEm),
                itens.Select(a => new AnaliseDaEtapaDto(
                    a.PublicId, EnumKebab.Texto(a.Escopo), a.Fonte, EnumKebab.Texto(a.Tipo), EnumKebab.Texto(a.Veredito),
                    a.DestinoNome, a.CustoUsd, a.RecebidaEm, new ContagemDaAnaliseDto(a.Critica, a.Alta, a.Media, a.Baixa, a.Info))).ToList());
        }

        public async Task<long?> LocalizarAnaliseAsync(long fluxoId, Guid analisePublicId, CancellationToken cancellationToken = default)
            => await contexto.Set<AnaliseEntity>()
                .Where(a => a.FluxoId == fluxoId && a.PublicId == analisePublicId)
                .Select(a => (long?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<PaginaDto<AchadoDto>> ListarAchadosAsync(
            long analiseId, SeveridadeAchado? severidade, PaginacaoDto paginacao, CancellationToken cancellationToken = default)
        {
            var consulta = contexto.Set<AchadoEntity>().Where(a => a.AnaliseId == analiseId);
            if (severidade is not null)
                consulta = consulta.Where(a => a.Severidade == severidade);

            var total = await consulta.CountAsync(cancellationToken);
            var pular = (long)(paginacao.Pagina - 1) * paginacao.TamanhoPagina;
            IReadOnlyList<AchadoDto> itens = [];
            if (pular < total)
            {
                var lidos = await consulta
                    .OrderBy(a => a.Severidade == SeveridadeAchado.Critica ? 0
                        : a.Severidade == SeveridadeAchado.Alta ? 1
                        : a.Severidade == SeveridadeAchado.Media ? 2
                        : a.Severidade == SeveridadeAchado.Baixa ? 3 : 4)
                    .ThenBy(a => a.Id)
                    .Skip((int)pular).Take(paginacao.TamanhoPagina)
                    .ToListAsync(cancellationToken);
                itens = lidos.Select(a => new AchadoDto(EnumKebab.Texto(a.Severidade), a.Titulo, a.RegraId, a.Arquivo, a.Linha, a.Descricao)).ToList();
            }

            return new PaginaDto<AchadoDto> { Itens = itens, Pagina = paginacao.Pagina, TamanhoPagina = paginacao.TamanhoPagina, Total = total };
        }
    }
}

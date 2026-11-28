using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Auditoria
{
    // X1: a única regra de visibilidade da trilha, usada por lista, contagem, filtros (e o detalhe da X1.4).
    // MOR vê tudo (inclusive AplicacaoId nulo); os demais, só registros de Aplicações em que têm algum papel.
    public interface IEscopoDeAuditoria
    {
        // `papeis` é a tabela de PapelAplicacao do mesmo contexto da consulta (vira subconsulta no banco).
        IQueryable<RegistroAuditoriaEntity> Restringir(IQueryable<RegistroAuditoriaEntity> registros, IQueryable<PapelAplicacaoEntity> papeis);
    }
}

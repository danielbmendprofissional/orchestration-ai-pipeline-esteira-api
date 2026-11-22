using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Aplicacoes
{
    // "ProjetoId depende de DependeDeProjetoId". Mesma Aplicação e ausência de ciclo são checados
    // pelo handler (precisam do grafo); aqui só o que a linha sozinha garante.
    public sealed class DependenciaProjetoEntity : EntityBase
    {
        private DependenciaProjetoEntity() { }

        public long ProjetoId { get; private set; }
        public long DependeDeProjetoId { get; private set; }

        public static Result<DependenciaProjetoEntity> Criar(long projetoId, long dependeDeProjetoId)
        {
            if (projetoId == dependeDeProjetoId)
                return new Erro("projeto.dependencia-invalida", "Um Projeto não pode depender dele mesmo.", TipoErro.Validation);

            return Result<DependenciaProjetoEntity>.Sucesso(new DependenciaProjetoEntity
            {
                ProjetoId = projetoId,
                DependeDeProjetoId = dependeDeProjetoId
            });
        }
    }
}

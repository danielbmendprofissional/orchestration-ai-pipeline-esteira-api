using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;
using Esteira.Domain.Common.Validations;
using Esteira.Domain.Common.ValueObjects;

namespace Esteira.Domain.Aplicacoes
{
    // Uma por Aplicação x Ambiente. Limite null = sem limite (a regra do piso fixo, 11a, é de outra task).
    public sealed class PoliticaSeveridadeEntity : EntityBase
    {
        private PoliticaSeveridadeEntity() { }

        public long AplicacaoId { get; private set; }
        public AmbientePolitica Ambiente { get; private set; }
        public int? Critica { get; private set; }
        public int? Alta { get; private set; }
        public int? Media { get; private set; }
        public int? Baixa { get; private set; }
        public ModoPolitica Modo { get; private set; }

        public static Result<PoliticaSeveridadeEntity> Criar(
            long aplicacaoId, AmbientePolitica ambiente, int? critica, int? alta, int? media, int? baixa, ModoPolitica modo)
        {
            Guard.AgainstInvalidEnum<AmbientePolitica>(ambiente, nameof(ambiente));
            Guard.AgainstInvalidEnum<ModoPolitica>(modo, nameof(modo));

            var limites = LimitesSeveridadeValueObject.Criar(critica, alta, media, baixa);
            if (!limites.IsSuccess)
                return Result<PoliticaSeveridadeEntity>.Falha(limites.Errors.ToArray());

            return Result<PoliticaSeveridadeEntity>.Sucesso(new PoliticaSeveridadeEntity
            {
                AplicacaoId = aplicacaoId,
                Ambiente = ambiente,
                Critica = critica,
                Alta = alta,
                Media = media,
                Baixa = baixa,
                Modo = modo
            });
        }

        // Política criada junto com a Aplicação: review avisa; qa/hml/producao bloqueiam com Crítica=0 e Alta=0.
        public static PoliticaSeveridadeEntity Padrao(long aplicacaoId, AmbientePolitica ambiente)
        {
            Guard.AgainstInvalidEnum<AmbientePolitica>(ambiente, nameof(ambiente));

            var temPiso = TemPiso(ambiente);
            return new PoliticaSeveridadeEntity
            {
                AplicacaoId = aplicacaoId,
                Ambiente = ambiente,
                Critica = temPiso ? 0 : null,
                Alta = temPiso ? 0 : null,
                Modo = temPiso ? ModoPolitica.Bloqueia : ModoPolitica.Avisa
            };
        }

        // Piso fixo (critério 11a): em qa/hml/producao só é permitido endurecer.
        private static bool TemPiso(AmbientePolitica ambiente) => ambiente != AmbientePolitica.Review;

        public Result Alterar(int? critica, int? alta, int? media, int? baixa, ModoPolitica modo)
        {
            Guard.AgainstInvalidEnum<ModoPolitica>(modo, nameof(modo));

            var limites = LimitesSeveridadeValueObject.Criar(critica, alta, media, baixa);
            if (!limites.IsSuccess)
                return Result.Falha(limites.Errors.ToArray());

            if (TemPiso(Ambiente) && (modo != ModoPolitica.Bloqueia || critica != 0 || alta != 0))
                return Result.Falha(new Erro(
                    "politica.abaixo-do-minimo",
                    "Em qa, hml e producao a política deve bloquear com Crítica e Alta em 0; só é permitido endurecer.",
                    TipoErro.Failure));

            Critica = critica;
            Alta = alta;
            Media = media;
            Baixa = baixa;
            Modo = modo;
            return Result.Sucesso();
        }
    }
}

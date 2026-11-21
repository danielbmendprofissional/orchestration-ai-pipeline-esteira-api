using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Aplicacoes
{
    // Erros e conversões compartilhados pelos handlers da Aplicação.
    public static class AplicacaoErros
    {
        public static readonly Erro SiglaDuplicada = new("aplicacao.sigla-duplicada", "Já existe uma Aplicação com esta sigla.", TipoErro.Conflict);

        public static readonly Erro GrupoJaVinculado = new("aplicacao.grupo-ja-vinculado", "Este grupo do GitLab já pertence a outra Aplicação.", TipoErro.Conflict);

        public static readonly Erro PoliticaNaoEncontrada = new("politica.nao-encontrada", "Política não encontrada.", TipoErro.NotFound);

        // Corrida: o índice único do banco pegou o que a verificação prévia não viu. Nome do índice: IX_<Tabela>_<Coluna>.
        public static Erro? DaViolacao(ViolacaoDeUnicidadeException violacao)
        {
            var restricao = violacao.Restricao ?? string.Empty;
            if (restricao.Contains("Sigla", StringComparison.OrdinalIgnoreCase))
                return SiglaDuplicada;
            if (restricao.Contains("GrupoGitLabId", StringComparison.OrdinalIgnoreCase))
                return GrupoJaVinculado;
            return null;
        }

        public static AplicacaoListaDto Lista(AplicacaoEntity a, int quantidadeProjetos = 0) => new()
        {
            Id = a.PublicId,
            Nome = a.Nome,
            Sigla = a.Sigla,
            Grupo = a.GrupoGitLabCaminho,
            QuantidadeProjetos = quantidadeProjetos,
            Ativa = a.Ativa
        };

        public static PoliticaDto Dto(PoliticaSeveridadeEntity p) => new()
        {
            Id = p.PublicId,
            Ambiente = EnumKebab.Texto(p.Ambiente),
            Critica = p.Critica,
            Alta = p.Alta,
            Media = p.Media,
            Baixa = p.Baixa,
            Modo = EnumKebab.Texto(p.Modo)
        };
    }
}

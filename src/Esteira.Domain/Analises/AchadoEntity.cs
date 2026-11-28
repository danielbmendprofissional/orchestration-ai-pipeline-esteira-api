using System.Text.RegularExpressions;
using Esteira.Domain.Common.Base;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Domain.Analises
{
    // Entrada de um Achado (texto de terceiro: saida de ferramenta ou de agente de IA). Validado em AnaliseEntity.Criar.
    public sealed record NovoAchado(SeveridadeAchado Severidade, string Titulo, string? RegraId, string? Arquivo, int? Linha, string? Descricao);

    // Achado de uma Analise (E2). Texto puro, nunca interpretado nem logado.
    public sealed partial class AchadoEntity : EntityBase
    {
        public const int MaxTitulo = 300, MaxRegraId = 200, MaxArquivo = 500, MaxDescricao = 2000, MaxChave = 300;

        private AchadoEntity() { }

        public long AnaliseId { get; private set; }
        public SeveridadeAchado Severidade { get; private set; }
        public string Titulo { get; private set; } = string.Empty;
        public string? RegraId { get; private set; }
        public string? Arquivo { get; private set; }
        public int? Linha { get; private set; }
        public string? Descricao { get; private set; }
        // `<fonte>:<regra_id>` ou `<fonte>:<titulo em minusculas, espacos colapsados>`. Calculada aqui, nunca vem do cliente.
        public string ChaveDoTipo { get; private set; } = string.Empty;

        internal static Result<AchadoEntity> Criar(string fonte, NovoAchado novo)
        {
            if (!Enum.IsDefined(novo.Severidade))
                return Invalido("A severidade do Achado é desconhecida.");
            if (string.IsNullOrWhiteSpace(novo.Titulo) || novo.Titulo.Length > MaxTitulo)
                return Invalido("O título do Achado deve ter de 1 a 300 caracteres.");
            if (novo.RegraId is { Length: > MaxRegraId })
                return Invalido("O regra_id do Achado excede 200 caracteres.");
            if (novo.Arquivo is { Length: > MaxArquivo })
                return Invalido("O arquivo do Achado excede 500 caracteres.");
            if (novo.Linha is < 1)
                return Invalido("A linha do Achado deve ser maior ou igual a 1.");
            if (novo.Descricao is { Length: > MaxDescricao })
                return Invalido("A descrição do Achado excede 2000 caracteres.");

            return Result<AchadoEntity>.Sucesso(new AchadoEntity
            {
                Severidade = novo.Severidade,
                Titulo = novo.Titulo,
                RegraId = novo.RegraId,
                Arquivo = novo.Arquivo,
                Linha = novo.Linha,
                Descricao = novo.Descricao,
                ChaveDoTipo = CalcularChave(fonte, novo)
            });
        }

        private static string CalcularChave(string fonte, NovoAchado novo)
        {
            var complemento = string.IsNullOrWhiteSpace(novo.RegraId)
                ? Espacos().Replace(novo.Titulo, " ").Trim().ToLowerInvariant()
                : novo.RegraId;
            var chave = $"{fonte}:{complemento}";
            return chave.Length > MaxChave ? chave[..MaxChave] : chave;
        }

        // Mensagem sem o texto do Achado (texto de terceiro nao volta em erro).
        private static Erro Invalido(string mensagem) => new("achado.invalido", mensagem, TipoErro.Validation);

        [GeneratedRegex(@"\s+")]
        private static partial Regex Espacos();
    }
}

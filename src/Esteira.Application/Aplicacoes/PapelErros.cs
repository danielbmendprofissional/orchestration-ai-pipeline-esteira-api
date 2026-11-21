using Esteira.Application.Common.Extensions;
using Esteira.Application.Interfaces.UnitOfWork;
using Esteira.Domain.Aplicacoes;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Aplicacoes
{
    // Erros e conversões compartilhados pelos handlers de papel (A1.5).
    public static class PapelErros
    {
        // Failure (422), como projeto.fora-do-grupo: o corpo é válido, mas referencia quem a Esteira não conhece.
        public static readonly Erro PessoaDesconhecida = new("papel.pessoa-desconhecida", "Esta pessoa ainda não entrou na Esteira: peça para ela fazer login antes de vincular o papel.", TipoErro.Failure);

        public static readonly Erro Duplicado = new("papel.duplicado", "A pessoa já tem este papel na Aplicação.", TipoErro.Conflict);

        public static readonly Erro NaoEncontrado = new("papel.nao-encontrado", "Papel não encontrado na Aplicação.", TipoErro.NotFound);

        // Corrida: o índice único (Aplicação, Usuário, Papel) pegou o que a verificação prévia não viu.
        public static Erro? DaViolacao(ViolacaoDeUnicidadeException violacao)
            => (violacao.Restricao ?? string.Empty).Contains("PapelAplicacao", StringComparison.OrdinalIgnoreCase) ? Duplicado : null;

        public static object Estado(PapelAplicacaoEntity p) => new { p.Usuario, Papel = EnumKebab.Texto(p.Papel) };
    }
}

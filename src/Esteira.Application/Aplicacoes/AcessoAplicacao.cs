using Esteira.Domain.Aplicacoes;

namespace Esteira.Application.Aplicacoes
{
    // O que a pessoa logada tem sobre uma Aplicação: papéis vinculados e a role global `mor`.
    public sealed record AcessoAplicacao(bool EhMor, IReadOnlyList<PapelAplicacao> Papeis)
    {
        public bool TemPapel => Papeis.Count > 0;

        public bool Pode(OperacaoAplicacao operacao) => EhMor || operacao switch
        {
            OperacaoAplicacao.Ver => TemPapel,
            OperacaoAplicacao.EditarCadastro => Papeis.Contains(PapelAplicacao.Analista),
            OperacaoAplicacao.EditarPolitica => Papeis.Contains(PapelAplicacao.AprovadorTecnico),
            _ => false
        };
    }
}

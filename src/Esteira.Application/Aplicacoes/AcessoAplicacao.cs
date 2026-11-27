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
            OperacaoAplicacao.AbrirReview => Papeis.Contains(PapelAplicacao.Membro) || Papeis.Contains(PapelAplicacao.AprovadorTecnico),
            OperacaoAplicacao.DecidirFatia => Papeis.Contains(PapelAplicacao.AprovadorTecnico),
            OperacaoAplicacao.AbrirEntrega => Papeis.Contains(PapelAplicacao.Membro),
            _ => false
        };
    }
}

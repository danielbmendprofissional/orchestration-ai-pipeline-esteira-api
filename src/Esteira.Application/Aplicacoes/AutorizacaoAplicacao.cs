using Esteira.Application.Interfaces.Aplicacoes;
using Esteira.Application.Interfaces.Usuario;
using Esteira.Domain.Common.Resultados;

namespace Esteira.Application.Aplicacoes
{
    // Única fonte da regra de acesso à Aplicação (papéis da pessoa + role `mor`), usada pelos handlers e pelo /permissoes.
    public sealed class AutorizacaoAplicacao(IUsuarioAtual usuario, IAcessoAplicacaoQuery query) : IAutorizacaoAplicacao
    {
        public static readonly Erro NaoEncontrada = new("aplicacao.nao-encontrada", "Aplicação não encontrada.", TipoErro.NotFound);

        public static readonly Erro PapelInsuficiente = new("acesso.papel-insuficiente", "Seu papel não permite esta operação.", TipoErro.Forbidden);

        public async Task<AcessoAplicacao> ObterAcessoAsync(long aplicacaoId, CancellationToken cancellationToken = default)
        {
            var papeis = await query.PapeisAsync(aplicacaoId, usuario.Id, cancellationToken);
            return new AcessoAplicacao(usuario.EhMor, papeis);
        }

        public async Task<Result> AutorizarAsync(long aplicacaoId, OperacaoAplicacao operacao, CancellationToken cancellationToken = default)
        {
            var acesso = await ObterAcessoAsync(aplicacaoId, cancellationToken);

            if (!acesso.EhMor && !acesso.TemPapel)
                return Result.Falha(NaoEncontrada);

            return acesso.Pode(operacao) ? Result.Sucesso() : Result.Falha(PapelInsuficiente);
        }

        public async Task<Result<PermissoesAplicacao>> PermissoesAsync(long aplicacaoId, CancellationToken cancellationToken = default)
        {
            var acesso = await ObterAcessoAsync(aplicacaoId, cancellationToken);

            if (!acesso.EhMor && !acesso.TemPapel)
                return Result<PermissoesAplicacao>.Falha(NaoEncontrada);

            return Result<PermissoesAplicacao>.Sucesso(new PermissoesAplicacao(
                PodeEditar: acesso.Pode(OperacaoAplicacao.EditarAplicacao),
                PodeEditarCadastro: acesso.Pode(OperacaoAplicacao.EditarCadastro),
                PodeEditarPolitica: acesso.Pode(OperacaoAplicacao.EditarPolitica),
                PodeGerenciarPapeis: acesso.Pode(OperacaoAplicacao.GerenciarPapeis)));
        }

        public bool PodeCriar() => usuario.EhMor;
    }
}
